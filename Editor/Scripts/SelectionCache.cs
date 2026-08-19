using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Caches the current Selection as HashSets, rebuilt only on selectionChanged - row draw
    /// callbacks fire on every repaint, so checking Selection.assetGUIDs/entityIds directly there
    /// would allocate and scan on every row, every frame.
    /// </summary>
    internal static class SelectionCache
    {
        private static readonly HashSet<string> _selectedAssetGuids = new();
        private static readonly HashSet<EntityId> _selectedEntityIds = new();
        private static bool _dirty = true;

        static SelectionCache()
        {
            Selection.selectionChanged += () => _dirty = true;
        }

        public static bool IsAssetSelected(string guid)
        {
            EnsureFresh();
            return _selectedAssetGuids.Contains(guid);
        }

        public static bool IsObjectSelected(int instanceId)
        {
            EnsureFresh();
            return _selectedEntityIds.Contains(instanceId);
        }

        private static void EnsureFresh()
        {
            if (!_dirty) return;
            _dirty = false;

            _selectedAssetGuids.Clear();
            foreach (string guid in Selection.assetGUIDs)
                _selectedAssetGuids.Add(guid);

            _selectedEntityIds.Clear();
            foreach (EntityId id in Selection.entityIds)
                _selectedEntityIds.Add(id);
        }
    }
}
