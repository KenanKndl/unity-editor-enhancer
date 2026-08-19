using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Stores pinned folder paths locally to this machine (EditorPrefs), under a key scoped
    /// to the project. string.GetHashCode() can vary per process, so a stable hash
    /// (FNV-1a) is used instead.
    /// </summary>
    public static class ProjectBookmarks
    {
        private static List<string> _paths;
        private static string _prefsKey;

        private static string PrefsKey
        {
            get
            {
                if (_prefsKey == null)
                    _prefsKey = "LenzDev_ProjectBookmarks_" + StableHash(Application.dataPath);
                return _prefsKey;
            }
        }

        public static IReadOnlyList<string> Paths
        {
            get
            {
                if (_paths == null) Load();
                return _paths;
            }
        }

        public static bool Contains(string path)
        {
            if (_paths == null) Load();
            return _paths.Contains(path);
        }

        public static void Add(string path)
        {
            if (_paths == null) Load();
            if (string.IsNullOrEmpty(path) || _paths.Contains(path)) return;
            _paths.Add(path);
            Save();
        }

        public static void Remove(string path)
        {
            if (_paths == null) Load();
            if (_paths.Remove(path)) Save();
        }

        public static void Reorder(int fromIndex, int toIndex)
        {
            if (_paths == null) Load();
            if (fromIndex < 0 || fromIndex >= _paths.Count) return;
            toIndex = Mathf.Clamp(toIndex, 0, _paths.Count - 1);
            string moved = _paths[fromIndex];
            _paths.RemoveAt(fromIndex);
            _paths.Insert(toIndex, moved);
            Save();
        }

        private static void Load()
        {
            string raw = EditorPrefs.GetString(PrefsKey, "");
            _paths = string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split('|').Where(p => !string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p)).ToList();
        }

        private static void Save()
        {
            EditorPrefs.SetString(PrefsKey, string.Join("|", _paths));
        }

        private static string StableHash(string input)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261;
                const uint fnvPrime = 16777619;
                uint hash = fnvOffset;
                foreach (char c in input)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }
                return hash.ToString("x8");
            }
        }
    }
}
