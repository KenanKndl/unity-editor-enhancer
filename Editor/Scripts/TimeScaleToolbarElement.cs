#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Adds a Time.timeScale control to the main Editor toolbar, docked right next to the
    /// Play/Pause/Step buttons. Uses Unity 6.3's public MainToolbarElement API - unlike the
    /// Project/Hierarchy bars, the main toolbar has a real supported extension point, so this
    /// needs no Harmony patching. Only compiled on 6.3+, since the API doesn't exist before that
    /// even though the package's own minimum supported version is 6000.0.
    ///
    /// A MainToolbarDropdown (rather than an inline MainToolbarSlider) is used deliberately: the
    /// inline slider's only way to add exact numeric entry is UI Toolkit's built-in
    /// showInputField, which places the number box in an awkward, disconnected spot next to the
    /// handle - a dropdown that opens a small IMGUI popup gives full control over the layout
    /// instead, matching the rest of the package's existing dark minimal popups.
    /// </summary>
    internal static class TimeScaleToolbarElement
    {
        [MainToolbarElement("Editor Customizer/Time Scale",
            defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 1)]
        private static MainToolbarElement CreateElement()
        {
            var content = new MainToolbarContent("⏱",
                "Time Scale - click to adjust Time.timeScale");
            // New elements default to hidden until the user opts in via the toolbar's own
            // customize UI - show it out of the box instead, since that's the whole point of
            // adding it.
            return new MainToolbarDropdown(content, TimeScalePopup.Show) { displayed = true };
        }
    }

    /// <summary>
    /// Small dark popup (matches ColorPickerPopup's minimal style) for setting Time.timeScale:
    /// an EditorGUILayout.Slider (which already renders a label, a drag slider, AND an editable
    /// number field in one row - no custom numeric-entry widget needed) plus a row of speed
    /// presets. Reads Time.timeScale fresh every OnGUI call rather than caching it, so it stays
    /// correct even if something external changes it while the popup happens to be open.
    /// </summary>
    internal class TimeScalePopup : PopupWindowContent
    {
        private const float Width = 220f;
        private const float Padding = 12f;
        private const float MinScale = 0f;
        private const float MaxScale = 4f;

        private static readonly (float value, string label)[] Presets =
        {
            (0.25f, "0.25x"), (0.5f, "0.5x"), (1f, "1x"), (2f, "2x"), (4f, "4x")
        };

        private static readonly Color BackgroundColor = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color LightTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        private GUIStyle _labelStyle;

        public static void Show(Rect activatorRect)
        {
            PopupWindow.Show(activatorRect, new TimeScalePopup());
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(Width, 96f);
        }

        public override void OnGUI(Rect rect)
        {
            if (_labelStyle == null)
                _labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = LightTextColor } };

            EditorGUI.DrawRect(rect, BackgroundColor);
            GUILayout.BeginArea(new Rect(Padding, Padding, rect.width - Padding * 2, rect.height - Padding * 2));

            EditorGUILayout.LabelField("Time Scale", _labelStyle);
            GUILayout.Space(6);

            // Same labelWidth fix as ColorPickerPopup's text-style rows: this popup is narrower
            // than EditorGUIUtility.labelWidth's host-view-based default, which would otherwise
            // squeeze the slider's own numeric field down to a couple of characters.
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70f;
            float newValue = EditorGUILayout.Slider("Scale", Time.timeScale, MinScale, MaxScale);
            EditorGUIUtility.labelWidth = prevLabelWidth;
            if (!Mathf.Approximately(newValue, Time.timeScale))
                Time.timeScale = newValue;

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Presets.Length; i++)
            {
                GUIStyle style = Presets.Length == 1 ? EditorStyles.miniButton
                    : i == 0 ? EditorStyles.miniButtonLeft
                    : i == Presets.Length - 1 ? EditorStyles.miniButtonRight
                    : EditorStyles.miniButtonMid;

                if (GUILayout.Button(Presets[i].label, style))
                    Time.timeScale = Presets[i].value;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
#endif
