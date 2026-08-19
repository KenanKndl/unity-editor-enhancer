using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.Networking;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Shared update-check/install logic - checks the package's distribution repo for newer git
    /// tags and installs one via the Package Manager. Static so PackageUpdaterWindow and
    /// ColorPaletteAssetEditor's embedded status row share one in-flight check/install.
    /// </summary>
    internal static class PackageUpdateChecker
    {
        private const string PackageName = "com.lenzdev.unity-editor-enhancer";
        private const string GitHubOwner = "KenanKndl";
        private const string GitHubRepo = "unity-editor-enhancer";
        private const string TagsApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepo + "/tags";
        private const string RepoGitUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepo + ".git";

        public static string InstalledVersion { get; private set; } = "unknown";
        public static string LatestVersion { get; private set; }
        public static string StatusMessage { get; private set; }
        public static bool IsChecking { get; private set; }
        public static bool IsUpdating { get; private set; }
        public static bool IsBusy => IsChecking || IsUpdating;
        public static bool UpdateAvailable => !string.IsNullOrEmpty(LatestVersion) && IsNewer(LatestVersion, InstalledVersion);

        /// <summary>Raised whenever any state above changes - callers Repaint() in response.</summary>
        public static event Action Changed;

        private static bool _installedVersionLoaded;
        private static string _latestTag;
        private static UnityWebRequest _checkRequest;
        private static AddRequest _addRequest;

        public static void EnsureInstalledVersionLoaded()
        {
            if (_installedVersionLoaded) return;
            RefreshInstalledVersion();
        }

        public static void RefreshInstalledVersion()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == PackageName);
            InstalledVersion = info != null ? info.version : "not installed";
            _installedVersionLoaded = true;
        }

        public static void StartCheck()
        {
            if (IsBusy) return;

            IsChecking = true;
            StatusMessage = null;
            Changed?.Invoke();

            _checkRequest = UnityWebRequest.Get(TagsApiUrl);
            _checkRequest.SetRequestHeader("User-Agent", "UnityEditorEnhancer-UpdateChecker");
            _checkRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            _checkRequest.SendWebRequest();

            EditorApplication.update += PollCheckRequest;
        }

        private static void PollCheckRequest()
        {
            if (_checkRequest != null && !_checkRequest.isDone)
            {
                Changed?.Invoke();
                return;
            }

            EditorApplication.update -= PollCheckRequest;
            IsChecking = false;

            if (_checkRequest != null)
            {
                if (_checkRequest.result != UnityWebRequest.Result.Success)
                    StatusMessage = $"Update check failed: {_checkRequest.error}";
                else
                    ParseLatestTag(_checkRequest.downloadHandler.text);

                _checkRequest.Dispose();
                _checkRequest = null;
            }

            Changed?.Invoke();
        }

        public static void StartUpdate()
        {
            if (string.IsNullOrEmpty(_latestTag) || IsBusy) return;

            IsUpdating = true;
            StatusMessage = null;
            Changed?.Invoke();

            _addRequest = Client.Add($"{RepoGitUrl}#{_latestTag}");
            EditorApplication.update += PollAddRequest;
        }

        private static void PollAddRequest()
        {
            if (_addRequest != null && !_addRequest.IsCompleted)
            {
                Changed?.Invoke();
                return;
            }

            EditorApplication.update -= PollAddRequest;
            IsUpdating = false;

            if (_addRequest != null)
            {
                if (_addRequest.Status == StatusCode.Success)
                {
                    StatusMessage = $"Updated to {_addRequest.Result.version}.";
                    RefreshInstalledVersion();
                }
                else
                {
                    StatusMessage = $"Update failed: {_addRequest.Error?.message}";
                }

                _addRequest = null;
            }

            Changed?.Invoke();
        }

        // JsonUtility can't deserialize a top-level JSON array; a regex scan for "name" is simpler.
        private static void ParseLatestTag(string json)
        {
            MatchCollection matches = Regex.Matches(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");

            string bestTag = null;
            (int major, int minor, int patch) bestVersion = default;
            bool found = false;

            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;
                if (!TryParseVersion(tag, out var version)) continue;

                if (!found || CompareVersions(version, bestVersion) > 0)
                {
                    found = true;
                    bestVersion = version;
                    bestTag = tag;
                }
            }

            if (found)
            {
                _latestTag = bestTag;
                LatestVersion = $"{bestVersion.major}.{bestVersion.minor}.{bestVersion.patch}";
            }
            else
            {
                _latestTag = null;
                LatestVersion = null;
                StatusMessage = "No published releases found yet.";
            }
        }

        private static bool TryParseVersion(string raw, out (int major, int minor, int patch) version)
        {
            version = default;
            if (string.IsNullOrEmpty(raw)) return false;

            Match match = Regex.Match(raw.TrimStart('v', 'V'), @"^(\d+)\.(\d+)\.(\d+)");
            if (!match.Success) return false;

            version = (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
            return true;
        }

        private static int CompareVersions((int major, int minor, int patch) a, (int major, int minor, int patch) b)
        {
            if (a.major != b.major) return a.major.CompareTo(b.major);
            if (a.minor != b.minor) return a.minor.CompareTo(b.minor);
            return a.patch.CompareTo(b.patch);
        }

        private static bool IsNewer(string latest, string installed)
        {
            if (!TryParseVersion(latest, out var latestVersion)) return false;
            if (!TryParseVersion(installed, out var installedVersion)) return true;
            return CompareVersions(latestVersion, installedVersion) > 0;
        }
    }
}
