using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Uses Harmony to patch Unity's internal ProjectBrowser, opening up space for the pin bar
    /// right below its own toolbar (the top +/search row) and drawing into that space.
    /// Since ProjectBrowser doesn't offer a public extension point, this technique can break
    /// across Unity version updates; every step is therefore guarded by try/catch and
    /// silently disables itself (with just a warning) if it fails.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectNavigationBarPatch
    {
        public const float BarHeight = 22f;
        private const string HarmonyId = "com.lenzdev.unity-editor-enhancer.navigationbar";

        private static float _toolbarHeight = 21f;
        private static bool _patchApplied;

        private static FieldInfo _listAreaRectField;
        private static FieldInfo _treeViewRectField;
        private static FieldInfo _listHeaderRectField;

        static ProjectNavigationBarPatch()
        {
            try
            {
                // Removes any previous patch first - domain reload can leave native trampolines
                // stacking on recompile otherwise.
                var harmony = new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
                ApplyPatch(harmony);

                AssemblyReloadEvents.beforeAssemblyReload += () =>
                {
                    try { harmony.UnpatchAll(HarmonyId); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[LenzDev] Failed to unpatch navigation bar cleanly before reload: " + e.Message);
                    }
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LenzDev] Navigation bar could not be integrated with this Unity version, " +
                                  "the feature has been disabled: " + e.Message);
            }
        }

        private static void ApplyPatch(Harmony harmony)
        {
            Type projectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null)
                throw new Exception("UnityEditor.ProjectBrowser type not found.");

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            _listAreaRectField = projectBrowserType.GetField("m_ListAreaRect", flags);
            _treeViewRectField = projectBrowserType.GetField("m_TreeViewRect", flags);
            _listHeaderRectField = projectBrowserType.GetField("m_ListHeaderRect", flags);

            var toolbarHeightProp = projectBrowserType.GetProperty("k_ToolbarHeight",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (toolbarHeightProp != null)
                _toolbarHeight = (float)toolbarHeightProp.GetValue(null);

            MethodInfo calculateRects = projectBrowserType.GetMethod("CalculateRects", flags);
            MethodInfo onGui = projectBrowserType.GetMethod("OnGUI", flags);

            if (_listAreaRectField == null || _treeViewRectField == null || _listHeaderRectField == null ||
                calculateRects == null || onGui == null)
                throw new Exception("One of the expected internal fields/methods was not found.");

            harmony.Patch(calculateRects, postfix: new HarmonyMethod(typeof(ProjectNavigationBarPatch), nameof(CalculateRectsPostfix)));
            harmony.Patch(onGui, postfix: new HarmonyMethod(typeof(ProjectNavigationBarPatch), nameof(OnGUIPostfix)));

            _patchApplied = true;
        }

        private static void CalculateRectsPostfix(object __instance)
        {
            if (!_patchApplied) return;

            ShiftRect(_listAreaRectField, __instance, shrink: true);
            ShiftRect(_treeViewRectField, __instance, shrink: true);
            ShiftRect(_listHeaderRectField, __instance, shrink: false);
        }

        private static void ShiftRect(FieldInfo field, object instance, bool shrink)
        {
            Rect r = (Rect)field.GetValue(instance);
            r.y += BarHeight;
            if (shrink) r.height -= BarHeight;
            field.SetValue(instance, r);
        }

        private static void OnGUIPostfix(object __instance)
        {
            if (!_patchApplied) return;

            var window = __instance as EditorWindow;
            if (window == null) return;

            Rect barRect = new Rect(0f, _toolbarHeight, window.position.width, BarHeight);
            ProjectNavigationBarDrawer.Draw(barRect, window);
        }
    }
}
