using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>Thin UI wrapper around PackageUpdateChecker's shared state.</summary>
    internal class PackageUpdaterWindow : EditorWindow
    {
        [MenuItem("Tools/LenzDev/Editor Enhancer/Check for Updates...")]
        private static void Open()
        {
            var window = GetWindow<PackageUpdaterWindow>(true, "Editor Enhancer Updates");
            window.minSize = new Vector2(380, 210);
        }

        private void OnEnable()
        {
            PackageUpdateChecker.EnsureInstalledVersionLoaded();
            PackageUpdateChecker.Changed += Repaint;
        }

        private void OnDisable()
        {
            PackageUpdateChecker.Changed -= Repaint;
        }

        private void OnGUI()
        {
            bool busy = PackageUpdateChecker.IsBusy;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Unity Editor Enhancer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Installed version: {PackageUpdateChecker.InstalledVersion}");
            EditorGUILayout.LabelField($"Latest version: {PackageUpdateChecker.LatestVersion ?? "unknown (check for updates)"}");

            EditorGUILayout.Space(8);

            if (busy)
            {
                Rect barRect = EditorGUILayout.GetControlRect(false, 18f);
                // Indeterminate pulse - neither request type exposes real progress.
                float pulse = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.75f, 1f);
                EditorGUI.ProgressBar(barRect, pulse, PackageUpdateChecker.IsChecking ? "Checking for updates..." : "Updating...");
                EditorGUILayout.Space(8);
            }

            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Check for Updates"))
                    PackageUpdateChecker.StartCheck();

                bool updateAvailable = PackageUpdateChecker.UpdateAvailable;
                using (new EditorGUI.DisabledScope(!updateAvailable))
                {
                    if (GUILayout.Button(updateAvailable ? $"Update to {PackageUpdateChecker.LatestVersion}" : "Update"))
                        PackageUpdateChecker.StartUpdate();
                }
            }

            if (!string.IsNullOrEmpty(PackageUpdateChecker.StatusMessage))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(PackageUpdateChecker.StatusMessage, MessageType.Info);
            }
        }
    }
}
