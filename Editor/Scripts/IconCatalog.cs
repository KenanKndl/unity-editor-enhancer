namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Curated, grouped list of Unity's built-in editor icon names for the ColorPaletteAsset
    /// Icons tab. Names are verified against EditorGUIUtility.IconContent and may need
    /// re-checking after a major Unity upgrade, since built-in icon names occasionally change.
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
            new Category("Transform Tools", new[]
            {
                "MoveTool", "RotateTool", "ScaleTool", "RectTool", "TransformTool",
                "ViewToolMove", "ViewToolOrbit", "ViewToolZoom"
            }),
            new Category("Physics", new[]
            {
                "Rigidbody Icon", "Rigidbody2D Icon", "BoxCollider Icon", "SphereCollider Icon",
                "CapsuleCollider Icon", "MeshCollider Icon", "WheelCollider Icon", "TerrainCollider Icon",
                "CharacterController Icon", "BoxCollider2D Icon", "CircleCollider2D Icon", "PolygonCollider2D Icon",
                "EdgeCollider2D Icon", "CapsuleCollider2D Icon", "HingeJoint Icon", "SpringJoint Icon",
                "FixedJoint Icon", "ConfigurableJoint Icon", "HingeJoint2D Icon", "SpringJoint2D Icon",
                "DistanceJoint2D Icon", "TargetJoint2D Icon", "RelativeJoint2D Icon", "FixedJoint2D Icon",
                "FrictionJoint2D Icon", "WheelJoint2D Icon", "ConstantForce Icon", "ConstantForce2D Icon", "Cloth Icon"
            }),
            new Category("Rendering", new[]
            {
                "MeshRenderer Icon", "MeshFilter Icon", "SkinnedMeshRenderer Icon", "SpriteRenderer Icon",
                "SpriteMask Icon", "LineRenderer Icon", "TrailRenderer Icon", "BillboardRenderer Icon",
                "ParticleSystem Icon", "ParticleSystemForceField Icon", "VisualEffect Icon", "LODGroup Icon",
                "ReflectionProbe Icon", "LightProbeGroup Icon", "LightProbeProxyVolume Icon",
                "OcclusionArea Icon", "OcclusionPortal Icon", "Projector Icon", "FlareLayer Icon"
            }),
            new Category("World & Navigation", new[]
            {
                "Terrain Icon", "Tree Icon", "WindZone Icon", "Grid Icon", "Tilemap Icon", "TilemapRenderer Icon",
                "NavMeshAgent Icon", "NavMeshObstacle Icon", "OffMeshLink Icon"
            }),
            new Category("Audio & Video", new[]
            {
                "AudioSource Icon", "AudioListener Icon", "AudioReverbZone Icon", "AudioLowPassFilter Icon",
                "AudioHighPassFilter Icon", "AudioEchoFilter Icon", "AudioChorusFilter Icon",
                "AudioDistortionFilter Icon", "AudioReverbFilter Icon", "VideoPlayer Icon"
            }),
            new Category("UI (uGUI)", new[]
            {
                "Canvas Icon", "CanvasGroup Icon", "CanvasScaler Icon", "GraphicRaycaster Icon", "RectTransform Icon",
                "Image Icon", "RawImage Icon", "Text Icon", "Button Icon", "Toggle Icon", "ToggleGroup Icon",
                "Slider Icon", "Scrollbar Icon", "ScrollRect Icon", "InputField Icon", "Dropdown Icon", "Mask Icon",
                "RectMask2D Icon", "ContentSizeFitter Icon", "HorizontalLayoutGroup Icon", "VerticalLayoutGroup Icon",
                "GridLayoutGroup Icon", "LayoutElement Icon", "AspectRatioFitter Icon", "EventSystem Icon",
                "StandaloneInputModule Icon", "Selectable Icon"
            }),
            new Category("Assets", new[]
            {
                "Material Icon", "Shader Icon", "cs Script Icon", "Texture Icon", "Texture2D Icon", "AudioClip Icon",
                "AnimationClip Icon", "Font Icon", "PhysicsMaterial2D Icon", "Avatar Icon", "SceneAsset Icon",
                "GameObject Icon", "Prefab Icon", "ScriptableObject Icon", "Animator Icon", "AnimatorController Icon"
            }),
            new Category("Gizmos", new[]
            {
                "AreaLight Gizmo", "AudioSource Gizmo", "Camera Gizmo", "DirectionalLight Gizmo", "DiscLight Gizmo",
                "LensFlare Gizmo", "LightProbeGroup Gizmo", "LightProbeProxyVolume Gizmo", "Main Light Gizmo",
                "ParticleSystem Gizmo", "ParticleSystemForceField Gizmo", "PointLight Gizmo", "Projector Gizmo",
                "ReflectionProbe Gizmo", "SpotLight Gizmo", "VisualEffect Gizmo", "WindZone Gizmo"
            }),
            new Category("Folders", new[]
            {
                "Folder Icon", "FolderEmpty Icon"
            }),
            new Category("Status", new[]
            {
                "console.warnicon", "console.erroricon", "console.infoicon",
                "greenLight", "orangeLight", "redLight",
                "Favorite Icon", "LockIcon", "LockIcon-On", "VisibilityOn", "VisibilityOff", "Linked", "Unlinked"
            }),
            new Category("Toolbar", new[]
            {
                "Toolbar Plus", "Toolbar Minus", "_Popup", "PlayButton", "PauseButton", "StepButton",
                "Search Icon", "Settings Icon", "FilterByType", "FilterByLabel"
            }),
            new Category("Windows", new[]
            {
                "UnityEditor.SceneView", "UnityEditor.GameView", "UnityEditor.ConsoleWindow",
                "UnityEditor.InspectorWindow", "UnityEditor.AnimationWindow", "UnityEditor.ProfilerWindow",
                "UnityEditor.HierarchyWindow"
            }),
            new Category("Primitives", new[]
            {
                "PreMatCube", "PreMatSphere", "PreMatCylinder", "PreMatQuad"
            })
        };
    }
}
