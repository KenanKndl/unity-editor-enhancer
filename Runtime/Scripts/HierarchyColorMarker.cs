using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Invisible data holder for a GameObject's Hierarchy row color.
    /// Unity refuses to attach components defined in an "Editor" folder to a GameObject
    /// ("Can't add script behaviour because it is an editor script"), so this type has to live
    /// in a Runtime-eligible location to be addable at all. That means it CAN compile into player
    /// builds - HierarchyMarkerBuildStripper (Editor-only) strips every instance from every scene
    /// during the build process so nothing actually ships.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class HierarchyColorMarker : MonoBehaviour
    {
        public Color color = Color.white;
    }
}
