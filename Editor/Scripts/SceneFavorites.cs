using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Stores favorited scene paths locally to this machine (EditorPrefs), under a key scoped
    /// to the project. Same stable-hash-per-project approach as ProjectBookmarks.
    /// </summary>
    internal static class SceneFavorites
    {
        private static HashSet<string> _paths;
        private static string _prefsKey;

        private static string PrefsKey
        {
            get
            {
                if (_prefsKey == null)
                    _prefsKey = "LenzDev_SceneFavorites_" + StableHash(Application.dataPath);
                return _prefsKey;
            }
        }

        public static bool IsFavorite(string path)
        {
            if (_paths == null) Load();
            return _paths.Contains(path);
        }

        public static void SetFavorite(string path, bool favorite)
        {
            if (_paths == null) Load();
            bool changed = favorite ? _paths.Add(path) : _paths.Remove(path);
            if (changed) Save();
        }

        public static void Toggle(string path)
        {
            SetFavorite(path, !IsFavorite(path));
        }

        private static void Load()
        {
            string raw = EditorPrefs.GetString(PrefsKey, "");
            _paths = string.IsNullOrEmpty(raw)
                ? new HashSet<string>()
                : new HashSet<string>(raw.Split('|').Where(p => !string.IsNullOrEmpty(p)));
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
