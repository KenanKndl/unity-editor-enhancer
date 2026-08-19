using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    internal readonly struct PinInfo
    {
        public readonly string Id;
        public readonly string DisplayName;

        public PinInfo(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Stores pinned GameObjects as GlobalObjectId strings (not the objects/instance IDs
    /// themselves), so pins survive scene unload/reload and can still be listed - just
    /// unresolved - while their scene isn't currently open. The object's name is cached
    /// alongside the id purely for display, since an unresolved pin has no live object to read
    /// a name from. Same stable-hash-per-project EditorPrefs approach as
    /// ProjectBookmarks/SceneFavorites.
    /// </summary>
    internal static class HierarchyPins
    {
        private const char EntrySeparator = '|';
        private const char FieldSeparator = '\u001F'; // unit separator, won't appear in a GameObject name

        private static List<PinInfo> _pins;
        private static string _prefsKey;

        private static string PrefsKey
        {
            get
            {
                if (_prefsKey == null)
                    _prefsKey = "LenzDev_HierarchyPins_" + StableHash(Application.dataPath);
                return _prefsKey;
            }
        }

        public static IReadOnlyList<PinInfo> Pins
        {
            get
            {
                if (_pins == null) Load();
                return _pins;
            }
        }

        public static bool Contains(GameObject go)
        {
            if (go == null) return false;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            if (_pins == null) Load();
            return _pins.Any(p => p.Id == id);
        }

        public static void Add(GameObject go)
        {
            if (go == null) return;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            if (_pins == null) Load();
            if (_pins.Any(p => p.Id == id)) return;
            _pins.Add(new PinInfo(id, go.name));
            Save();
        }

        public static void Remove(string id)
        {
            if (_pins == null) Load();
            if (_pins.RemoveAll(p => p.Id == id) > 0) Save();
        }

        public static void Reorder(int fromIndex, int toIndex)
        {
            if (_pins == null) Load();
            if (fromIndex < 0 || fromIndex >= _pins.Count) return;
            toIndex = Mathf.Clamp(toIndex, 0, _pins.Count - 1);
            PinInfo moved = _pins[fromIndex];
            _pins.RemoveAt(fromIndex);
            _pins.Insert(toIndex, moved);
            Save();
        }

        /// <summary>
        /// Resolves a pin to its live GameObject. Returns null if the object's scene isn't
        /// currently loaded (or the object was deleted) - not an error, just "not available now".
        /// </summary>
        public static GameObject TryResolve(string id)
        {
            if (!GlobalObjectId.TryParse(id, out GlobalObjectId gid))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        }

        /// <summary>
        /// The scene a pin belongs to, read from the asset database (works even when that scene
        /// isn't currently loaded, since it's asset metadata rather than runtime scene state).
        /// </summary>
        public static string GetSceneName(string id)
        {
            if (!GlobalObjectId.TryParse(id, out GlobalObjectId gid))
                return "?";

            string path = AssetDatabase.GUIDToAssetPath(gid.assetGUID);
            return string.IsNullOrEmpty(path) ? "?" : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        private static void Load()
        {
            string raw = EditorPrefs.GetString(PrefsKey, "");
            _pins = new List<PinInfo>();
            if (string.IsNullOrEmpty(raw)) return;

            foreach (string entry in raw.Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] fields = entry.Split(FieldSeparator);
                if (fields.Length < 1 || string.IsNullOrEmpty(fields[0])) continue;
                string displayName = fields.Length > 1 ? fields[1] : "(unknown)";
                _pins.Add(new PinInfo(fields[0], displayName));
            }
        }

        private static void Save()
        {
            string raw = string.Join(EntrySeparator.ToString(),
                _pins.Select(p => p.Id + FieldSeparator + p.DisplayName));
            EditorPrefs.SetString(PrefsKey, raw);
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
