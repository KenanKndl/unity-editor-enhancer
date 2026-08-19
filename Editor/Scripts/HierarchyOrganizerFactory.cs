using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Creates the Hierarchy-only organizational GameObjects (dividers/headers) used by the
    /// pin bar's action buttons. Always appended as the active scene's last root object, so
    /// repeated use builds a predictable, append-only order.
    /// </summary>
    internal static class HierarchyOrganizerFactory
    {
        public static void CreateDivider() => Create(HierarchyOrganizerMarker.OrganizerKind.Divider, "Divider");
        public static void CreateHeader() => Create(HierarchyOrganizerMarker.OrganizerKind.Header, "New Header");

        private static void Create(HierarchyOrganizerMarker.OrganizerKind kind, string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetAsLastSibling();

            var marker = Undo.AddComponent<HierarchyOrganizerMarker>(go);
            marker.kind = kind;

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            HierarchyColorOverlay.ClearCache();
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
