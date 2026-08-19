using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FolderTreeLines
{
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.18f);

    static FolderTreeLines()
    {
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }

    private static void OnProjectWindowItemGUI(string guid, Rect rect)
    {
        if (!FolderTreeSettings.ShowLines) return;

        // Only the left-hand folder tree draws hierarchy lines. Grid view rows are taller than
        // 20px, and their x is just the icon's column position in the grid - not an indent
        // level - so without this check lines were being drawn on non-first-column grid icons
        // in the right-hand Assets content pane, which has nothing to do with folder depth.
        if (rect.height > 20f) return;
        if (rect.x <= 16f) return;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;

        if (path.StartsWith("Packages") || path == "Packages") return;

        int depth = GetFolderDepth(path);
        if (depth <= 0) return;

        float indentWidth = 14f;
        float baseIndent = 16f;

        bool hasSubFolders = AssetDatabase.GetSubFolders(path).Length > 0;

        for (int i = 1; i <= depth; i++)
        {
            float currentX = baseIndent + (i * indentWidth) - 10f;

            Rect verticalLine = new Rect(currentX, rect.y, 1f, rect.height);
            EditorGUI.DrawRect(verticalLine, LineColor);

            if (i == depth)
            {
                float horizontalLength = hasSubFolders ? 5f : 11f;

                Rect horizontalLine = new Rect(currentX, rect.y + rect.height / 2f, horizontalLength, 1f);
                EditorGUI.DrawRect(horizontalLine, LineColor);
            }
        }
    }

    private static int GetFolderDepth(string path)
    {
        int depth = 0;
        string current = path;
        while (true)
        {
            current = System.IO.Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(current) || current == "Assets" || current == "Packages")
                break;
            depth++;
        }
        return depth;
    }
}