using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Draws pinned folders as chips in the space opened up by ProjectNavigationBarPatch.
    /// Supports pinning via drag-and-drop, navigating via left click, removing via right click,
    /// reordering by dragging a chip, and scrolling via arrow buttons when chips overflow.
    /// </summary>
    internal static class ProjectNavigationBarDrawer
    {
        private const float ChipHeight = 18f;
        private const float ChipPadding = 8f;
        private const float ChipSpacing = 4f;
        private const float IconSize = 14f;
        private const float ArrowWidth = 16f;
        private const float ScrollStep = 70f;
        private const float DragThreshold = 4f;

        private static GUIStyle _chipLabelStyle;
        private static GUIStyle _hintStyle;
        private static GUIStyle _arrowStyle;
        private static readonly GUIContent _reusableContent = new GUIContent();

        private static readonly List<Rect> _chipRects = new List<Rect>();
        private static float _scrollOffset;

        private static int _pressIndex = -1;
        private static Vector2 _pressMousePos;
        private static bool _isDraggingChip;

        public static void Draw(Rect rect, EditorWindow window)
        {
            EnsureStyles();

            Color bgColor = EditorGUIUtility.isProSkin
                ? new Color(0.19f, 0.19f, 0.19f, 1f)
                : new Color(0.78f, 0.78f, 0.78f, 1f);
            EditorGUI.DrawRect(rect, bgColor);

            Color separator = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.4f)
                : new Color(0f, 0f, 0f, 0.25f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), separator);

            var bookmarks = ProjectBookmarks.Paths;

            if (bookmarks.Count == 0)
            {
                GUI.Label(new Rect(rect.x + ChipPadding, rect.y, rect.width - ChipPadding * 2f, rect.height),
                    "Drag a folder here to pin it", _hintStyle);
                HandleDragAndDrop(rect);
                return;
            }

            float[] widths = new float[bookmarks.Count];
            float totalWidth = 0f;
            for (int i = 0; i < bookmarks.Count; i++)
            {
                widths[i] = ComputeChipWidth(bookmarks[i]);
                totalWidth += widths[i] + (i > 0 ? ChipSpacing : 0f);
            }

            float availableWidth = rect.width - ChipPadding * 2f;
            bool needsScroll = totalWidth > availableWidth;
            float contentX = rect.x + ChipPadding;
            float contentWidth = availableWidth;

            if (needsScroll)
            {
                contentX += ArrowWidth;
                contentWidth -= ArrowWidth * 2f;
            }

            float maxScroll = Mathf.Max(0f, totalWidth - contentWidth);
            _scrollOffset = needsScroll ? Mathf.Clamp(_scrollOffset, 0f, maxScroll) : 0f;

            // Pass 1: precompute all chip rects (needed to detect drag targets)
            _chipRects.Clear();
            float x = -_scrollOffset;
            for (int i = 0; i < bookmarks.Count; i++)
            {
                // Mirror horizontally: the first pinned chip sits flush against the right edge
                // and later chips extend leftward, instead of the default left-to-right strip.
                float mirroredX = contentWidth - x - widths[i];
                _chipRects.Add(new Rect(mirroredX, (rect.height - ChipHeight) / 2f, widths[i], ChipHeight));
                x += widths[i] + ChipSpacing;
            }

            GUI.BeginGroup(new Rect(contentX, rect.y, contentWidth, rect.height));
            for (int i = 0; i < bookmarks.Count; i++)
            {
                DrawChipVisual(_chipRects[i], bookmarks[i]);
                HandleChipInteraction(_chipRects[i], bookmarks[i], i);
            }
            GUI.EndGroup();

            if (needsScroll)
            {
                // Mirrored layout: chip 0 sits at the right edge and later chips extend
                // leftward, so the physical left arrow now reveals more of the strip (increases
                // scrollOffset) and the physical right arrow scrolls back toward chip 0
                // (decreases scrollOffset) - the opposite of the un-mirrored left-to-right case.
                DrawArrow(new Rect(rect.x + ChipPadding, rect.y, ArrowWidth, rect.height), false,
                    _scrollOffset < maxScroll - 0.01f, () => _scrollOffset = Mathf.Min(maxScroll, _scrollOffset + ScrollStep));
                DrawArrow(new Rect(rect.xMax - ChipPadding - ArrowWidth, rect.y, ArrowWidth, rect.height), true,
                    _scrollOffset > 0.01f, () => _scrollOffset = Mathf.Max(0f, _scrollOffset - ScrollStep));
            }

            HandleDragAndDrop(rect);
        }

        private static void EnsureStyles()
        {
            if (_chipLabelStyle == null)
            {
                _chipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }
            _chipLabelStyle.normal.textColor = FolderColorOverlay.DefaultTextColor;

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Italic
                };
            }
            Color hintColor = FolderColorOverlay.DefaultTextColor;
            hintColor.a = 0.5f;
            _hintStyle.normal.textColor = hintColor;

            if (_arrowStyle == null)
            {
                _arrowStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                };
            }
        }

        private static float ComputeChipWidth(string path)
        {
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            _reusableContent.text = name;
            Vector2 size = _chipLabelStyle.CalcSize(_reusableContent);
            return IconSize + 4f + size.x + ChipPadding * 2f;
        }

        private static void DrawChipVisual(Rect chipRect, string path)
        {
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            bool hovering = chipRect.Contains(Event.current.mousePosition);

            Texture2D roundedTex = RoundedTextureProvider.Get();
            Color chipColor = hovering
                ? (EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0.12f))
                : (EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f));

            if (roundedTex != null)
            {
                var prev = GUI.color;
                GUI.color = chipColor;
                GUI.DrawTexture(chipRect, roundedTex);
                GUI.color = prev;
            }
            else
            {
                EditorGUI.DrawRect(chipRect, chipColor);
            }

            Texture folderIcon = EditorGUIUtility.FindTexture("Folder Icon");
            if (folderIcon != null)
            {
                Rect iconRect = new Rect(chipRect.x + ChipPadding, chipRect.y + (ChipHeight - IconSize) / 2f, IconSize, IconSize);

                Color iconColor = Color.white;
                if (FolderMetaData.TryGetFolderData(path, out Color customColor, out _, out bool hasColor,
                    out _, out _, out _, out _) && hasColor)
                    iconColor = customColor;

                var prevColor = GUI.color;
                GUI.color = iconColor;
                GUI.DrawTexture(iconRect, folderIcon);
                GUI.color = prevColor;
            }

            _reusableContent.text = name;
            Rect labelRect = new Rect(chipRect.x + ChipPadding + IconSize + 4f, chipRect.y,
                chipRect.width - ChipPadding - IconSize - 4f, ChipHeight);
            GUI.Label(labelRect, name, _chipLabelStyle);
        }

        private static void HandleChipInteraction(Rect rect, string path, int index)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    _pressIndex = index;
                    _pressMousePos = e.mousePosition;
                    _isDraggingChip = false;
                    e.Use();
                }
                else if (e.button == 1)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Remove"), false, () => ProjectBookmarks.Remove(path));
                    menu.ShowAsContext();
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && _pressIndex == index)
            {
                if (!_isDraggingChip && (e.mousePosition - _pressMousePos).magnitude > DragThreshold)
                    _isDraggingChip = true;

                if (_isDraggingChip)
                {
                    for (int j = 0; j < _chipRects.Count; j++)
                    {
                        if (j != _pressIndex && _chipRects[j].Contains(e.mousePosition))
                        {
                            ProjectBookmarks.Reorder(_pressIndex, j);
                            _pressIndex = j;
                            break;
                        }
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _pressIndex == index)
            {
                if (!_isDraggingChip)
                    NavigateTo(path);

                _pressIndex = -1;
                _isDraggingChip = false;
                e.Use();
            }
        }

        private static void DrawArrow(Rect rect, bool pointRight, bool enabled, System.Action onClick)
        {
            bool hovering = enabled && rect.Contains(Event.current.mousePosition);
            if (hovering)
            {
                Color highlight = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.1f);
                EditorGUI.DrawRect(rect, highlight);
            }

            Color prevContentColor = GUI.contentColor;
            GUI.contentColor = enabled
                ? (EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.15f, 0.15f, 0.15f))
                : (EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f, 0.3f) : new Color(0.15f, 0.15f, 0.15f, 0.3f));
            GUI.Label(rect, pointRight ? "▸" : "◂", _arrowStyle);
            GUI.contentColor = prevContentColor;

            if (enabled && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                onClick();
                Event.current.Use();
                EditorApplication.RepaintProjectWindow();
            }
        }

        private static void NavigateTo(string path)
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (folder == null)
            {
                // Folder may have been deleted or moved, clear the invalid pin
                ProjectBookmarks.Remove(path);
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static void HandleDragAndDrop(Rect rect)
        {
            Event e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!rect.Contains(e.mousePosition)) return;

            bool hasFolder = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(p))
                {
                    hasFolder = true;
                    break;
                }
            }

            if (!hasFolder)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            if (e.type == EventType.DragPerform)
            {
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string p = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(p))
                        ProjectBookmarks.Add(p);
                }
                DragAndDrop.AcceptDrag();
            }

            e.Use();
        }
    }
}
