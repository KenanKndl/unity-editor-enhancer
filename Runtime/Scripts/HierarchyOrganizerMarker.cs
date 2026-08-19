using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Marks a GameObject as a Hierarchy-only organizational row (a divider or header), drawn
    /// specially by HierarchyColorOverlay. Must live outside any "Editor" folder - Unity refuses
    /// to attach an Editor-assembly component to a GameObject. Stripped from every scene at build
    /// time by HierarchyMarkerBuildStripper.
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
