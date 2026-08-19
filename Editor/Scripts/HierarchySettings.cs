using UnityEditor;

namespace LenzDev.EditorCustomizer
{
    public enum HierarchyDividerStyle
    {
        Line,
        Band
    }

    public static class HierarchySettings
    {
        private const string SingleRowKey = "LenzDev_Hierarchy_SingleRowColoring";
        private const string PanelLeftInsetKey = "LenzDev_Hierarchy_PanelLeftInset";
        private const string DividerStyleKey = "LenzDev_Hierarchy_DividerStyle";

        /// <summary>
        /// When true, only the explicitly colored GameObject's row is colored. When false,
        /// child GameObjects inherit the nearest colored ancestor's color, same as
        /// FolderTreeSettings.SingleColumnColoring does for folders.
        /// </summary>
        public static bool SingleRowColoring
        {
            get => EditorPrefs.GetBool(SingleRowKey, false);
            set => EditorPrefs.SetBool(SingleRowKey, value);
        }

        /// <summary>
        /// How far in from the Hierarchy window's left edge the row color overlay reaches.
        /// This is the tree view's own panel margin (a thin darker frame sits outside it), which
        /// isn't derivable from any row's rect - it varies with Unity's skin/DPI scaling, so it's
        /// exposed as a manual slider instead of a guessed constant.
        /// </summary>
        public static float PanelLeftInset
        {
            get => EditorPrefs.GetFloat(PanelLeftInsetKey, 32f);
            set => EditorPrefs.SetFloat(PanelLeftInsetKey, value);
        }

        /// <summary>
        /// Visual style used to draw every Divider-kind HierarchyOrganizerMarker row: a thin
        /// centered line, or a full-row colored band. Global (applies to all dividers at once)
        /// rather than per-object, matching every other appearance setting in this class.
        /// </summary>
        public static HierarchyDividerStyle DividerStyle
        {
            get => (HierarchyDividerStyle)EditorPrefs.GetInt(DividerStyleKey, (int)HierarchyDividerStyle.Line);
            set => EditorPrefs.SetInt(DividerStyleKey, (int)value);
        }
    }
}
