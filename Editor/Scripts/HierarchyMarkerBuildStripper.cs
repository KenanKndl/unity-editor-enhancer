using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// HierarchyColorMarker only exists in the Editor assembly. Without this step, any scene
    /// containing a colored GameObject would ship with a "missing script" reference where the
    /// marker used to be, since the component's type disappears from the player build.
    /// </summary>
    internal class HierarchyMarkerBuildStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var marker in root.GetComponentsInChildren<HierarchyColorMarker>(true))
                {
                    Object.DestroyImmediate(marker);
                }

                foreach (var marker in root.GetComponentsInChildren<HierarchyOrganizerMarker>(true))
                {
                    Object.DestroyImmediate(marker);
                }
            }
        }
    }
}
