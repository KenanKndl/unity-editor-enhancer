namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Curated, grouped list of Unity's own built-in editor icon names, browsed from the
    /// ColorPaletteAsset's Icons tab to build a personal icon palette (ColorPaletteAsset.
    /// customIcons). Every name here was verified to resolve via EditorGUIUtility.IconContent on
    /// the Unity version this was written against - built-in icon names have changed across
    /// versions before (e.g. the classic "sv_label0".."sv_label7" were renamed to
    /// "sv_label_0".."sv_label_7"), so this list may need re-verifying on a major Unity upgrade.
    /// </summary>
    internal static class IconCatalog
    {
        internal readonly struct Category
        {
            public readonly string Name;
            public readonly string[] IconNames;

            public Category(string name, string[] iconNames)
            {
                Name = name;
                IconNames = iconNames;
            }
        }

        public static readonly Category[] Categories =
        {
            new Category("Labels", new[]
            {
                "sv_label_0", "sv_label_1", "sv_label_2", "sv_label_3",
                "sv_label_4", "sv_label_5", "sv_label_6", "sv_label_7"
            }),
            new Category("Markers", new[]
            {
                "sv_icon_dot0_pix16_gizmo", "sv_icon_dot1_pix16_gizmo", "sv_icon_dot2_pix16_gizmo", "sv_icon_dot3_pix16_gizmo",
                "sv_icon_dot4_pix16_gizmo", "sv_icon_dot5_pix16_gizmo", "sv_icon_dot6_pix16_gizmo", "sv_icon_dot7_pix16_gizmo",
                "sv_icon_dot8_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", "sv_icon_dot10_pix16_gizmo", "sv_icon_dot11_pix16_gizmo",
                "sv_icon_dot12_pix16_gizmo", "sv_icon_dot13_pix16_gizmo", "sv_icon_dot14_pix16_gizmo", "sv_icon_dot15_pix16_gizmo"
            }),
            new Category("Components", new[]
            {
                "Camera Icon", "Light Icon", "AudioSource Icon", "Rigidbody Icon",
                "BoxCollider Icon", "SphereCollider Icon", "CapsuleCollider Icon", "MeshRenderer Icon",
                "MeshFilter Icon", "SkinnedMeshRenderer Icon", "ParticleSystem Icon", "Canvas Icon",
                "RectTransform Icon", "Transform Icon", "Animator Icon", "AnimatorController Icon"
            }),
            new Category("Folders", new[]
            {
                "Folder Icon", "FolderEmpty Icon"
            }),
            new Category("Status", new[]
            {
                "console.warnicon", "console.erroricon", "console.infoicon"
            }),
            new Category("Toolbar", new[]
            {
                "Toolbar Plus", "Toolbar Minus", "_Popup"
            })
        };
    }
}
