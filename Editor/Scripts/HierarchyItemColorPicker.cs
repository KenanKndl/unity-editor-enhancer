using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Triggered whenever a GameObject is drawn in the Hierarchy window.
    /// Opens the color picker popup when a left click is detected while Alt (Option) is held.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyItemColorPicker
    {
        static HierarchyItemColorPicker()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
        }

        private static void OnGUI(int instanceID, Rect selectionRect)
        {
            Event e = Event.current;

            if (e.type != EventType.MouseDown || e.button != 0 || !e.alt ||
                !selectionRect.Contains(e.mousePosition))
            {
                return;
            }

            GameObject go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            if (go == null) return;

            ColorPickerPopup.Show(selectionRect, go);
            e.Use();
        }
    }
}
