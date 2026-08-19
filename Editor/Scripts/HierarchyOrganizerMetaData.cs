using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Mutates the header-only text styling fields on a GameObject's HierarchyOrganizerMarker.
    /// Mirrors HierarchyMetaData's Undo/dirty/notify pattern, kept separate since it edits fields
    /// specific to HierarchyOrganizerMarker rather than the generic HierarchyColorMarker.
    /// </summary>
    internal static class HierarchyOrganizerMetaData
    {
        public static void SetTextStyle(GameObject go, FontStyle style)
        {
            var organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer == null) return;

            Undo.RecordObject(organizer, "Set Header Font Style");
            organizer.textStyle = style;
            EditorUtility.SetDirty(organizer);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void SetTextAlign(GameObject go, HierarchyOrganizerMarker.TextAlign align)
        {
            var organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer == null) return;

            Undo.RecordObject(organizer, "Set Header Alignment");
            organizer.textAlign = align;
            EditorUtility.SetDirty(organizer);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void SetFontSize(GameObject go, int size)
        {
            var organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer == null) return;

            Undo.RecordObject(organizer, "Set Header Font Size");
            organizer.fontSize = size;
            EditorUtility.SetDirty(organizer);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void SetTextColor(GameObject go, Color color)
        {
            var organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer == null) return;

            Undo.RecordObject(organizer, "Set Header Text Color");
            organizer.hasCustomTextColor = true;
            organizer.textColor = color;
            EditorUtility.SetDirty(organizer);
            MarkSceneDirty(go);
            NotifyChanged();
        }

        public static void ClearTextColor(GameObject go)
        {
            var organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer == null) return;

            Undo.RecordObject(organizer, "Clear Header Text Color");
            organizer.hasCustomTextColor = false;
            EditorUtility.SetDirty(organizer);
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
