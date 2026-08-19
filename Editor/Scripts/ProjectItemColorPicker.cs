using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Triggered whenever a file/folder is drawn in the Project window.
    /// Opens the color picker popup when a left click is detected while Alt (Option) is held.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectItemColorPicker
    {
        static ProjectItemColorPicker()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            Event e = Event.current;

            if (e.type != EventType.MouseDown || e.button != 0 || !e.alt ||
                !selectionRect.Contains(e.mousePosition))
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Folders only - files keep Unity's default behavior.
            if (!AssetDatabase.IsValidFolder(path))
                return;

            ColorPickerPopup.Show(selectionRect, path);
            e.Use();
        }
    }
}