using System.Collections.Generic;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    // This file can live in either the Runtime or Editor folder (since we display it in the
    // Inspector, the ScriptableObject itself shouldn't depend on Editor, but the attribute isn't Editor-only).
    [CreateAssetMenu(fileName = "New Color Palette", menuName = "Editor Customizer/Color Palette")]
    public class ColorPaletteAsset : ScriptableObject
    {
        // Slightly transparent (100/255) rather than fully opaque - these are meant to read as
        // lightweight starting points to click and customize, not final swatch colors.
        private const float DefaultPresetAlpha = 100f / 255f;

        [Tooltip("Fixed / suggested default colors.")]
        public List<Color> defaultColors = new List<Color>
        {
            new Color(1f, 0f, 0f, DefaultPresetAlpha),   // red
            new Color(0f, 1f, 0f, DefaultPresetAlpha),   // green
            new Color(0f, 0f, 1f, DefaultPresetAlpha),   // blue
            new Color(1f, 1f, 0f, DefaultPresetAlpha),   // yellow
            new Color(0f, 1f, 1f, DefaultPresetAlpha),   // cyan
            new Color(1f, 0f, 1f, DefaultPresetAlpha),   // magenta
            new Color(1f, 1f, 1f, DefaultPresetAlpha),   // white
            new Color(0f, 0f, 0f, DefaultPresetAlpha),   // black
            new Color(1f, 0.5f, 0f, DefaultPresetAlpha), // orange
            new Color(0.5f, 0f, 1f, DefaultPresetAlpha)  // purple
        };

        [Tooltip("Custom colors the user has added to this palette.")]
        public List<Color> customColors = new List<Color>();

        [Tooltip("The currently selected color (other tools can read this).")]
        public Color selectedColor = Color.white;

        [Header("Folder Stats Settings")]
        [Tooltip("Background badge color of the stats label.")]
        public Color statsBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.15f);

        [Tooltip("Text color of the stats label.")]
        public Color statsTextColor = new Color(0.65f, 0.65f, 0.65f, 0.9f);

        [Header("Icon Palette")]
        [Tooltip("Built-in icon names picked from the Icons tab. Stored by name (not by Texture " +
                 "reference) since these are Editor-only built-in resources, resolved fresh via " +
                 "EditorGUIUtility.IconContent wherever they're used.")]
        public List<string> customIcons = new List<string>();
    }
}