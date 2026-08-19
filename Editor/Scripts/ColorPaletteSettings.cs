using UnityEditor;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Resolves the single ColorPaletteAsset every Project/Hierarchy overlay and popup in this
    /// package reads from when more than one exists in the project. Stored as a project-wide
    /// EditorBuildSettings config object, so the choice is shared across the whole team.
    /// </summary>
    [InitializeOnLoad]
    public static class ColorPaletteSettings
    {
        private const string ConfigKey = "LenzDev_EditorCustomizer_ActiveColorPalette";

        private static ColorPaletteAsset _cachedActive;
        private static bool _activeCacheValid;
        private static ColorPaletteAsset[] _cachedAll;

        static ColorPaletteSettings()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        /// <summary>
        /// The palette every overlay/popup should read from. Falls back to the project's only
        /// ColorPaletteAsset when none is explicitly set; null if none exists.
        /// </summary>
        public static ColorPaletteAsset Active
        {
            get
            {
                if (!_activeCacheValid)
                {
                    _cachedActive = Resolve();
                    _activeCacheValid = true;
                }
                return _cachedActive;
            }
        }

        /// <summary>True once more than one ColorPaletteAsset exists in the project.</summary>
        public static bool HasAmbiguity => FindAll().Length > 1;

        public static bool IsActive(ColorPaletteAsset palette) => palette != null && Active == palette;

        /// <summary>Explicitly assigns the project-wide active palette.</summary>
        public static void SetActive(ColorPaletteAsset palette)
        {
            if (palette == null) return;
            EditorBuildSettings.AddConfigObject(ConfigKey, palette, overwrite: true);
            _cachedActive = palette;
            _activeCacheValid = true;
        }

        public static void Invalidate()
        {
            _activeCacheValid = false;
            _cachedActive = null;
            _cachedAll = null;
        }

        private static ColorPaletteAsset Resolve()
        {
            if (EditorBuildSettings.TryGetConfigObject(ConfigKey, out ColorPaletteAsset assigned) && assigned != null)
                return assigned;

            ColorPaletteAsset[] all = FindAll();
            if (all.Length == 0) return null;

            if (all.Length == 1)
                EditorBuildSettings.AddConfigObject(ConfigKey, all[0], overwrite: true);

            return all[0];
        }

        private static ColorPaletteAsset[] FindAll()
        {
            if (_cachedAll != null) return _cachedAll;

            string[] guids = AssetDatabase.FindAssets("t:ColorPaletteAsset");
            _cachedAll = new ColorPaletteAsset[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _cachedAll[i] = AssetDatabase.LoadAssetAtPath<ColorPaletteAsset>(path);
            }
            return _cachedAll;
        }
    }
}
