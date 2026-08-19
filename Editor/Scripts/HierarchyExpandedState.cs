using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Reads which Hierarchy rows are currently expanded, via SceneHierarchy.GetExpandedIDs() -
    /// a public method declared on an internal class (UnityEditor.SceneHierarchy), so reaching
    /// it needs reflection, same as ProjectNavigationBarPatch already does for ProjectBrowser.
    /// Best effort only: if Unity's internals change shape, this just reports "nothing expanded"
    /// everywhere, which only affects the redrawn foldout arrow's direction, never functionality.
    /// </summary>
    internal static class HierarchyExpandedState
    {
        private static bool _reflectionFailed;
        private static Type _windowType;
        private static PropertyInfo _sceneHierarchyProp;
        private static MethodInfo _getExpandedIdsMethod;

        private static readonly HashSet<EntityId> _expandedIds = new HashSet<EntityId>();
        private static double _lastRefresh = -1;

        private static bool _collapseFailed;
        private static MethodInfo _setExpandedRecursiveMethod;
        private static bool _setExpandedRecursiveOnSceneHierarchy;

        public static bool IsExpanded(int instanceId)
        {
            RefreshIfNeeded();
            return _expandedIds.Contains(instanceId);
        }

        /// <summary>
        /// Collapses every root GameObject (and, recursively, all of their descendants) in the
        /// active scene, across every open Hierarchy window. Uses SceneHierarchy(Window)'s own
        /// public SetExpandedRecursive(int id, bool expand) - the exact declaring type isn't
        /// stable across Unity versions, so both plausible owners are tried once and the winner
        /// is cached. Same best-effort contract as the rest of this class: any failure just
        /// disables the feature (nothing to collapse), never throws.
        /// </summary>
        public static void CollapseAll()
        {
            if (_collapseFailed) return;

            try
            {
                if (_windowType == null)
                {
                    _windowType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                    _sceneHierarchyProp = _windowType?.GetProperty("sceneHierarchy", BindingFlags.Instance | BindingFlags.Public);
                }

                if (_windowType == null)
                {
                    _collapseFailed = true;
                    return;
                }

                if (_setExpandedRecursiveMethod == null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
                    _setExpandedRecursiveMethod = _windowType.GetMethod("SetExpandedRecursive", flags);
                    _setExpandedRecursiveOnSceneHierarchy = false;

                    if (_setExpandedRecursiveMethod == null)
                    {
                        Type sceneHierarchyType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");
                        _setExpandedRecursiveMethod = sceneHierarchyType?.GetMethod("SetExpandedRecursive", flags);
                        _setExpandedRecursiveOnSceneHierarchy = true;
                    }

                    if (_setExpandedRecursiveMethod == null)
                    {
                        _collapseFailed = true;
                        return;
                    }
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid()) return;

                GameObject[] roots = activeScene.GetRootGameObjects();

                foreach (UnityEngine.Object win in Resources.FindObjectsOfTypeAll(_windowType))
                {
                    object target = win;
                    if (_setExpandedRecursiveOnSceneHierarchy)
                    {
                        target = _sceneHierarchyProp?.GetValue(win);
                        if (target == null) continue;
                    }

                    foreach (GameObject root in roots)
                        _setExpandedRecursiveMethod.Invoke(target, new object[] { root.GetInstanceID(), false });
                }

                _lastRefresh = -1; // force IsExpanded's own cache to pick up the change on next repaint
                EditorApplication.RepaintHierarchyWindow();
            }
            catch
            {
                _collapseFailed = true;
            }
        }

        private static void RefreshIfNeeded()
        {
            if (_reflectionFailed) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRefresh < 0.1) return;
            _lastRefresh = now;

            try
            {
                if (_windowType == null)
                {
                    Assembly asm = typeof(Editor).Assembly;
                    _windowType = asm.GetType("UnityEditor.SceneHierarchyWindow");
                    Type sceneHierarchyType = asm.GetType("UnityEditor.SceneHierarchy");
                    _sceneHierarchyProp = _windowType?.GetProperty("sceneHierarchy", BindingFlags.Instance | BindingFlags.Public);
                    _getExpandedIdsMethod = sceneHierarchyType?.GetMethod("GetExpandedIDs", BindingFlags.Instance | BindingFlags.Public);

                    if (_windowType == null || _sceneHierarchyProp == null || _getExpandedIdsMethod == null)
                    {
                        _reflectionFailed = true;
                        return;
                    }
                }

                _expandedIds.Clear();
                foreach (UnityEngine.Object win in Resources.FindObjectsOfTypeAll(_windowType))
                {
                    object sceneHierarchy = _sceneHierarchyProp.GetValue(win);
                    if (sceneHierarchy == null) continue;

                    if (_getExpandedIdsMethod.Invoke(sceneHierarchy, null) is EntityId[] ids)
                    {
                        foreach (EntityId id in ids)
                            _expandedIds.Add(id);
                    }
                }
            }
            catch
            {
                _reflectionFailed = true;
            }
        }
    }
}
