using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Turns the Scene Header row's scene name into a clickable "quick switch" button: a hover
    /// highlight over the name plus a dropdown chevron right after it, both acting as one region.
    /// Detection works entirely through public API - the instanceID hierarchyWindowItemOnGUI
    /// passes for a Scene Header row is that Scene's own `handle`.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneQuickSwitchButton
    {
        private const float IconWidth = 16f;
        private const float IconPadding = 2f;
        private const float DropdownIconWidth = 12f;
        private const float DropdownPadding = 4f;

        private static GUIStyle _nameStyle;
        private static GUIStyle _dropdownStyle;
        private static readonly GUIContent _reusableContent = new GUIContent();

        static SceneQuickSwitchButton()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
        }

        private static void OnGUI(int instanceID, Rect rect)
        {
            Scene scene = FindSceneByHandle(instanceID);
            if (!scene.IsValid()) return;

            EnsureStyles();

            float nameX = rect.x + IconWidth + IconPadding;
            _reusableContent.text = scene.name;
            float nameWidth = _nameStyle.CalcSize(_reusableContent).x;

            Rect buttonRect = new Rect(nameX - 2f, rect.y,
                nameWidth + DropdownPadding + DropdownIconWidth + 4f, rect.height);

            bool hovering = buttonRect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint && hovering)
            {
                Color highlight = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.12f)
                    : new Color(0f, 0f, 0f, 0.1f);
                EditorGUI.DrawRect(buttonRect, highlight);
            }

            Rect dropdownRect = new Rect(nameX + nameWidth + DropdownPadding, rect.y, DropdownIconWidth, rect.height);
            GUI.Label(dropdownRect, "▾", _dropdownStyle);

            EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                buttonRect.Contains(Event.current.mousePosition))
            {
                // ShowAsDropDown (unlike PopupWindow.Show) needs the activator rect in screen
                // space, not GUI-local space.
                SceneQuickSwitchPopup.Show(GUIUtility.GUIToScreenRect(buttonRect), scene);
                Event.current.Use();
            }
        }

        private static Scene FindSceneByHandle(int handle)
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.handle == handle) return s;
            }
            return default;
        }

        private static void EnsureStyles()
        {
            if (_nameStyle == null)
                _nameStyle = new GUIStyle(EditorStyles.boldLabel);

            if (_dropdownStyle == null)
            {
                _dropdownStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }
    }
}
