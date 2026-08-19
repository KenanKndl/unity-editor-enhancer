using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Lets a consumer project check the package's dedicated distribution repo for newer git
    /// tags and install one via the Package Manager, without leaving the Editor. Meaningless in
    /// this dev repo (the package is embedded here, not installed via git), but that's expected -
    /// this window is for projects that added the package via git URL.
    /// </summary>
    internal class PackageUpdaterWindow : EditorWindow
    {
        private const string PackageName = "com.lenzdev.unity-editor-customizer";
        private const string GitHubOwner = "KenanKndl";
        private const string GitHubRepo = "unity-editor-enhancer";
        private const string TagsApiUrl = "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepo + "/tags";
        private const string RepoGitUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepo + ".git";

        [MenuItem("Tools/LenzDev/Editor Customizer/Check for Updates...")]
        private static void Open()
        {
            var window = GetWindow<PackageUpdaterWindow>(true, "Editor Customizer Updates");
            window.minSize = new Vector2(380, 210);
        }

        private string _installedVersion = "unknown";
        private string _latestTag;
        private string _latestVersion;
        private string _statusMessage;
        private bool _isChecking;
        private bool _isUpdating;

        private UnityWebRequest _checkRequest;
        private AddRequest _addRequest;

        private void OnEnable() => RefreshInstalledVersion();

        private void OnDisable()
        {
            EditorApplication.update -= PollCheckRequest;
            EditorApplication.update -= PollAddRequest;
        }

        private void RefreshInstalledVersion()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == PackageName);
            _installedVersion = info != null ? info.version : "not installed";
        }

        private void OnGUI()
        {
            bool busy = _isChecking || _isUpdating;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Unity Editor Customizer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Installed version: {_installedVersion}");
            EditorGUILayout.LabelField($"Latest version: {_latestVersion ?? "unknown (check for updates)"}");

            EditorGUILayout.Space(8);

            if (busy)
            {
                Rect barRect = EditorGUILayout.GetControlRect(false, 18f);
                // Neither UnityWebRequest nor the PackageManager AddRequest expose real progress,
                // so this is a deliberately indeterminate pulse rather than a fake percentage.
                float pulse = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.75f, 1f);
                EditorGUI.ProgressBar(barRect, pulse, _isChecking ? "Checking for updates..." : "Updating...");
                EditorGUILayout.Space(8);
            }

            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Check for Updates"))
                    StartCheck();

                bool updateAvailable = !string.IsNullOrEmpty(_latestVersion) && IsNewer(_latestVersion, _installedVersion);
                using (new EditorGUI.DisabledScope(!updateAvailable))
                {
                    if (GUILayout.Button(updateAvailable ? $"Update to {_latestVersion}" : "Update"))
                        StartUpdate();
                }
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void StartCheck()
        {
            _isChecking = true;
            _statusMessage = null;

            _checkRequest = UnityWebRequest.Get(TagsApiUrl);
            _checkRequest.SetRequestHeader("User-Agent", "UnityEditorCustomizer-UpdateChecker");
            _checkRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            _checkRequest.SendWebRequest();

            EditorApplication.update += PollCheckRequest;
        }

        private void PollCheckRequest()
        {
            if (_checkRequest != null && !_checkRequest.isDone)
            {
                Repaint();
                return;
            }

            EditorApplication.update -= PollCheckRequest;
            _isChecking = false;

            if (_checkRequest != null)
            {
                if (_checkRequest.result != UnityWebRequest.Result.Success)
                    _statusMessage = $"Update check failed: {_checkRequest.error}";
                else
                    ParseLatestTag(_checkRequest.downloadHandler.text);

                _checkRequest.Dispose();
                _checkRequest = null;
            }

            Repaint();
        }

        private void StartUpdate()
        {
            if (string.IsNullOrEmpty(_latestTag)) return;

            _isUpdating = true;
            _statusMessage = null;

            _addRequest = Client.Add($"{RepoGitUrl}#{_latestTag}");
            EditorApplication.update += PollAddRequest;
        }

        private void PollAddRequest()
        {
            if (_addRequest != null && !_addRequest.IsCompleted)
            {
                Repaint();
                return;
            }

            EditorApplication.update -= PollAddRequest;
            _isUpdating = false;

            if (_addRequest != null)
            {
                if (_addRequest.Status == StatusCode.Success)
                {
                    _statusMessage = $"Updated to {_addRequest.Result.version}.";
                    RefreshInstalledVersion();
                }
                else
                {
                    _statusMessage = $"Update failed: {_addRequest.Error?.message}";
                }

                _addRequest = null;
            }

            Repaint();
        }

        // GitHub's tags API returns a JSON array, which JsonUtility can't deserialize directly,
        // and each entry's "name" field is the only thing needed here - so a small regex scan
        // over the raw response is simpler and more robust than modeling the full response shape.
        private void ParseLatestTag(string json)
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
                _latestVersion = $"{bestVersion.major}.{bestVersion.minor}.{bestVersion.patch}";
            }
            else
            {
                _latestTag = null;
                _latestVersion = null;
                _statusMessage = "No published releases found yet.";
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
