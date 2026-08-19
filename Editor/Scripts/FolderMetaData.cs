using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace LenzDev.EditorCustomizer
{
    public static class FolderMetaData
    {
        private const string ColorKey = "LenzDevColor:";
        private const string StatsKey = "LenzDevStats:";
        private const string TextColorKey = "LenzDevTextColor:";
        private const string TextStyleKey = "LenzDevTextStyle:";
        private const string TextSizeKey = "LenzDevTextSize:";

        public static void SetFolderColor(string path, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            UpdateMeta(path, ColorKey, hex);
        }

        public static void ClearFolderColor(string path)
        {
            RemoveMeta(path, ColorKey);
        }

        public static void SetFolderStats(string path, bool show)
        {
            if (show) UpdateMeta(path, StatsKey, "1");
            else RemoveMeta(path, StatsKey);
        }

        public static void SetFolderTextColor(string path, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            UpdateMeta(path, TextColorKey, hex);
        }

        public static void ClearFolderTextColor(string path)
        {
            RemoveMeta(path, TextColorKey);
        }

        public static void SetFolderTextStyle(string path, FontStyle style)
        {
            UpdateMeta(path, TextStyleKey, ((int)style).ToString());
        }

        public static void SetFolderFontSize(string path, int size)
        {
            UpdateMeta(path, TextSizeKey, size.ToString());
        }

        public static bool TryGetFolderData(string path, out Color color, out bool showStats, out bool hasColor,
            out Color textColor, out bool hasTextColor, out FontStyle textStyle, out int fontSize)
        {
            color = Color.white;
            showStats = false;
            hasColor = false;
            textColor = Color.white;
            hasTextColor = false;
            textStyle = FontStyle.Normal;
            fontSize = 0;

            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.userData))
            {
                string[] parts = importer.userData.Split(';');
                foreach (var part in parts)
                {
                    if (part.StartsWith(ColorKey))
                    {
                        string hex = part.Substring(ColorKey.Length);
                        if (ColorUtility.TryParseHtmlString("#" + hex, out color))
                            hasColor = true;
                    }
                    else if (part.StartsWith(StatsKey))
                    {
                        showStats = part.Substring(StatsKey.Length) == "1";
                    }
                    else if (part.StartsWith(TextColorKey))
                    {
                        string hex = part.Substring(TextColorKey.Length);
                        if (ColorUtility.TryParseHtmlString("#" + hex, out textColor))
                            hasTextColor = true;
                    }
                    else if (part.StartsWith(TextStyleKey))
                    {
                        if (int.TryParse(part.Substring(TextStyleKey.Length), out int styleInt))
                            textStyle = (FontStyle)styleInt;
                    }
                    else if (part.StartsWith(TextSizeKey))
                    {
                        int.TryParse(part.Substring(TextSizeKey.Length), out fontSize);
                    }
                }
            }
            return hasColor || showStats;
        }

        private static void UpdateMeta(string path, string key, string value)
        {
            // We use delayCall to avoid an immediate reimport conflict inside the popup
            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null) return;

                string data = RemoveDataStr(importer.userData, key);
                importer.userData = string.IsNullOrEmpty(data) ? $"{key}{value}" : $"{data};{key}{value}";
                importer.SaveAndReimport();
                NotifyMetaWritten();
            };
        }

        private static void RemoveMeta(string path, string key)
        {
            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && !string.IsNullOrEmpty(importer.userData) && importer.userData.Contains(key))
                {
                    importer.userData = RemoveDataStr(importer.userData, key);
                    importer.SaveAndReimport();
                    NotifyMetaWritten();
                }
            };
        }

        // Since the actual write is deferred via delayCall, the cache-clear/repaint request must
        // only fire AFTER the data has actually been written to disk; otherwise the screen reads
        // and caches the previous (not-yet-written) data and ends up one step behind.
        private static void NotifyMetaWritten()
        {
            FolderColorOverlay.ClearColorCache();
            FolderColorOverlay.RequestStatsRefresh();
            EditorApplication.RepaintProjectWindow();
        }

        private static string RemoveDataStr(string userData, string key)
        {
            if (string.IsNullOrEmpty(userData)) return "";
            string[] parts = userData.Split(';');
            var result = new List<string>();
            foreach (var part in parts)
            {
                if (!part.StartsWith(key) && !string.IsNullOrEmpty(part))
                    result.Add(part);
            }
            return string.Join(";", result);
        }
    }
}