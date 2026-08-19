using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Invisible data holder marking a GameObject as a Hierarchy-only organizational row (a
    /// divider or a header), drawn specially by HierarchyColorOverlay. Lives in a
    /// Runtime-eligible location for the same reason as HierarchyColorMarker: Unity refuses to
    /// attach a component defined in an "Editor" folder to a GameObject. HierarchyMarkerBuildStripper
    /// strips every instance from every scene during the build process so nothing actually ships.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class HierarchyOrganizerMarker : MonoBehaviour
    {
        public enum OrganizerKind
        {
            Divider,
            Header
        }

        public enum TextAlign
        {
            Left,
            Center,
            Right
        }

        public OrganizerKind kind = OrganizerKind.Divider;

        // Header kind only - ignored for Divider.
        public FontStyle textStyle = FontStyle.Bold;
        public TextAlign textAlign = TextAlign.Center;
        public int fontSize = 12;
        public bool hasCustomTextColor;
        public Color textColor = Color.white;
    }
}
