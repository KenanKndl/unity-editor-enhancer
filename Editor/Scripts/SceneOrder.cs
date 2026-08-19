using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Stores a user-customized display order for scenes in the quick-switch popup (EditorPrefs,
    /// project-scoped - same stable-hash pattern as ProjectBookmarks/SceneFavorites/HierarchyPins).
    /// One master order list backs both the favorites and normal groups: Sort() filters it down
    /// to whichever set of paths the caller passes in, so dragging within one group never
    /// disturbs the other group's relative order.
    /// </summary>
    internal static class SceneOrder
    {
        private static List<string> _order;
        private static string _prefsKey;

        private static string PrefsKey
        {
            get
            {
                if (_prefsKey == null)
                    _prefsKey = "LenzDev_SceneOrder_" + StableHash(Application.dataPath);
                return _prefsKey;
            }
        }

        /// <summary>
        /// Returns paths in the user's saved custom order, with any paths not yet in that order
        /// (new scenes) appended alphabetically at the end.
        /// </summary>
        public static List<string> Sort(IEnumerable<string> paths)
        {
            if (_order == null) Load();

            var pathSet = new HashSet<string>(paths);
            var ordered = _order.Where(pathSet.Contains).ToList();

            var known = new HashSet<string>(ordered);
            var remaining = pathSet.Where(p => !known.Contains(p))
                .OrderBy(Path.GetFileNameWithoutExtension, System.StringComparer.OrdinalIgnoreCase);

            ordered.AddRange(remaining);
            return ordered;
        }

        public static void MoveBefore(string draggedPath, string targetPath)
        {
            if (draggedPath == targetPath) return;
            if (_order == null) Load();

            EnsurePresent(draggedPath);
            EnsurePresent(targetPath);

            _order.Remove(draggedPath);
            int targetIndex = _order.IndexOf(targetPath);
            _order.Insert(targetIndex, draggedPath);
            Save();
        }

        /// <summary>
        /// Same as MoveBefore, but inserts right after targetPath instead - needed to drop a
        /// dragged scene at the very end of a group, since there's no further path left to be
        /// "before" at that point.
        /// </summary>
        public static void MoveAfter(string draggedPath, string targetPath)
        {
            if (draggedPath == targetPath) return;
            if (_order == null) Load();

            EnsurePresent(draggedPath);
            EnsurePresent(targetPath);

            _order.Remove(draggedPath);
            int targetIndex = _order.IndexOf(targetPath);
            _order.Insert(targetIndex + 1, draggedPath);
            Save();
        }

        private static void EnsurePresent(string path)
        {
            if (!_order.Contains(path))
                _order.Add(path);
        }

        private static void Load()
        {
            string raw = EditorPrefs.GetString(PrefsKey, "");
            _order = string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split('|').Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private static void Save()
        {
            EditorPrefs.SetString(PrefsKey, string.Join("|", _order));
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
