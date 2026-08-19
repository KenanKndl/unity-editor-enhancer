using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    public static class HierarchyMetaData
    {
        public static bool TryGetColor(GameObject go, out Color color)
        {
            color = Color.white;
            if (go == null) return false;

            var marker = go.GetComponent<HierarchyColorMarker>();
            if (marker == null) return false;

            color = marker.color;
            return true;
        }

        public static void SetColor(GameObject go, Color color)
        {
            if (go == null) return;

            var marker = go.GetComponent<HierarchyColorMarker>();
            if (marker == null)
            {
                marker = Undo.AddComponent<HierarchyColorMarker>(go);
                marker.hideFlags = HideFlags.HideInInspector;
            }
            else
            {
                Undo.RecordObject(marker, "Set Hierarchy Color");
            }

            marker.color = color;
            EditorUtility.SetDirty(marker);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void ClearColor(GameObject go)
        {
            if (go == null) return;

            var marker = go.GetComponent<HierarchyColorMarker>();
            if (marker == null) return;

            Undo.DestroyObjectImmediate(marker);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        /// <summary>
        /// Overrides the GameObject's Hierarchy/Inspector icon - the exact same mechanism as
        /// clicking the icon swatch at the top of the Inspector. This is genuine engine-level
        /// GameObject state (an "m_Icon" field, not an EditorPrefs/session cache), so it survives
        /// scene save/reload like any other GameObject property; no marker component needed.
        /// </summary>
        public static void SetIcon(GameObject go, Texture2D icon)
        {
            if (go == null) return;

            Undo.RecordObject(go, "Set Icon");
            EditorGUIUtility.SetIconForObject(go, icon);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void ClearIcon(GameObject go)
        {
            if (go == null) return;

            Undo.RecordObject(go, "Clear Icon");
            EditorGUIUtility.SetIconForObject(go, null);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        private static void MarkSceneDirty(GameObject go)
        {
            if (!Application.isPlaying && go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static void NotifyChanged()
        {
            HierarchyColorOverlay.ClearCache();
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
