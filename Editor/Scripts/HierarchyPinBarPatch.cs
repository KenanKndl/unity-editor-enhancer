using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Uses Harmony to patch SceneHierarchy's OnGUI, shrinking the drawn Rect to free a strip
    /// above it for the pin bar. Guarded by try/catch and silently disables itself (with a
    /// warning) if it fails, since this technique can break across Unity version updates.
    /// </summary>
    [InitializeOnLoad]
    internal static class HierarchyPinBarPatch
    {
        public const float BarHeight = 22f;
        private const string HarmonyId = "com.lenzdev.unity-editor-enhancer.hierarchypinbar";

        private static bool _patchApplied;
        private static FieldInfo _editorWindowField;

        static HierarchyPinBarPatch()
        {
            try
            {
                var harmony = new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
                ApplyPatch(harmony);

                AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LenzDev] Hierarchy pin bar could not be integrated with this Unity version, " +
                                  "the feature has been disabled: " + e.Message);
            }
        }

        private static void ApplyPatch(Harmony harmony)
        {
            Type sceneHierarchyType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");
            if (sceneHierarchyType == null)
                throw new Exception("UnityEditor.SceneHierarchy type not found.");

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            MethodInfo onGui = sceneHierarchyType.GetMethod("OnGUI", flags);
            if (onGui == null)
                throw new Exception("SceneHierarchy.OnGUI(Rect) method not found.");

            _editorWindowField = sceneHierarchyType.GetField("m_EditorWindow", flags);
            if (_editorWindowField == null)
                throw new Exception("SceneHierarchy.m_EditorWindow field not found.");

            harmony.Patch(onGui, prefix: new HarmonyMethod(typeof(HierarchyPinBarPatch), nameof(OnGUIPrefix)));

            _patchApplied = true;
        }

        private static void OnGUIPrefix(object __instance, ref Rect rect)
        {
            if (!_patchApplied) return;

            Rect barRect = new Rect(rect.x, rect.y, rect.width, BarHeight);
            rect = new Rect(rect.x, rect.y + BarHeight, rect.width, rect.height - BarHeight);

            var window = _editorWindowField.GetValue(__instance) as EditorWindow;
            HierarchyPinBarDrawer.Draw(barRect, window);
        }
    }
}
