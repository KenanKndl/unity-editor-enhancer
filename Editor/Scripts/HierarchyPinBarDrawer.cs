using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Draws pinned GameObjects as icon-only chips in the space opened up by
    /// HierarchyPinBarPatch. Supports pinning via drag-and-drop from the Hierarchy, navigating
    /// via left click (only when the pin resolves - its scene is loaded), removing via right
    /// click regardless of resolved state, reordering by dragging a chip, and scrolling via
    /// arrow buttons when chips overflow. The name is shown only as a hover tooltip.
    /// </summary>
    internal static class HierarchyPinBarDrawer
    {
        private const float ChipSize = 20f;
        private const float ChipPadding = 8f;
        private const float ChipSpacing = 4f;
        private const float IconSize = 14f;
        private const float ArrowWidth = 16f;
        private const float ScrollStep = 60f;
        private const float DragThreshold = 4f;

        private const float ActionButtonSize = 20f;
        private const float ActionButtonSpacing = 4f;
        private const int ActionButtonCount = 3;
        private const float ActionAreaWidth =
            ActionButtonCount * ActionButtonSize + (ActionButtonCount - 1) * ActionButtonSpacing + ChipPadding * 2f;

        private static GUIStyle _hintStyle;
        private static GUIStyle _arrowStyle;

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

            Rect pinAreaRect = new Rect(rect.x, rect.y, rect.width - ActionAreaWidth, rect.height);
            Rect actionAreaRect = new Rect(pinAreaRect.xMax, rect.y, ActionAreaWidth, rect.height);

            if (Event.current.type == EventType.Repaint)
            {
                Color divider = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.12f);
                EditorGUI.DrawRect(new Rect(actionAreaRect.x, rect.y + 4f, 1f, rect.height - 8f), divider);
            }
            DrawActionButtons(actionAreaRect);

            IReadOnlyList<PinInfo> pins = HierarchyPins.Pins;

            if (pins.Count == 0)
            {
                GUI.Label(new Rect(pinAreaRect.x + ChipPadding, pinAreaRect.y, pinAreaRect.width - ChipPadding * 2f, pinAreaRect.height),
                    "Drag a GameObject here to pin it", _hintStyle);
                HandleDragAndDrop(pinAreaRect);
                return;
            }

            float chipStep = ChipSize + ChipSpacing;
            float totalWidth = pins.Count * chipStep - ChipSpacing;

            float availableWidth = pinAreaRect.width - ChipPadding * 2f;
            bool needsScroll = totalWidth > availableWidth;
            float contentX = pinAreaRect.x + ChipPadding;
            float contentWidth = availableWidth;

            if (needsScroll)
            {
                contentX += ArrowWidth;
                contentWidth -= ArrowWidth * 2f;
            }

            float maxScroll = Mathf.Max(0f, totalWidth - contentWidth);
            _scrollOffset = needsScroll ? Mathf.Clamp(_scrollOffset, 0f, maxScroll) : 0f;

            _chipRects.Clear();
            float x = -_scrollOffset;
            for (int i = 0; i < pins.Count; i++)
            {
                _chipRects.Add(new Rect(x, (pinAreaRect.height - ChipSize) / 2f, ChipSize, ChipSize));
                x += chipStep;
            }

            GUI.BeginGroup(new Rect(contentX, pinAreaRect.y, contentWidth, pinAreaRect.height));
            for (int i = 0; i < pins.Count; i++)
                DrawChip(_chipRects[i], pins[i], i);
            GUI.EndGroup();

            if (needsScroll)
            {
                DrawArrow(new Rect(pinAreaRect.x + ChipPadding, pinAreaRect.y, ArrowWidth, pinAreaRect.height), false,
                    _scrollOffset > 0.01f, () => _scrollOffset = Mathf.Max(0f, _scrollOffset - ScrollStep));
                DrawArrow(new Rect(pinAreaRect.xMax - ChipPadding - ArrowWidth, pinAreaRect.y, ArrowWidth, pinAreaRect.height), true,
                    _scrollOffset < maxScroll - 0.01f, () => _scrollOffset = Mathf.Min(maxScroll, _scrollOffset + ScrollStep));
            }

            HandleDragAndDrop(pinAreaRect);
        }

        private static void DrawActionButtons(Rect areaRect)
        {
            float y = areaRect.y + (areaRect.height - ActionButtonSize) / 2f;
            float step = ActionButtonSize + ActionButtonSpacing;
            float x = areaRect.x + ChipPadding;

            DrawIconButton(new Rect(x, y, ActionButtonSize, ActionButtonSize),
                "Add Divider", DrawDividerIcon, HierarchyOrganizerFactory.CreateDivider);

            DrawIconButton(new Rect(x + step, y, ActionButtonSize, ActionButtonSize),
                "Add Header", DrawHeaderIcon, HierarchyOrganizerFactory.CreateHeader);

            DrawIconButton(new Rect(x + step * 2f, y, ActionButtonSize, ActionButtonSize),
                "Collapse All", DrawCollapseIcon, HierarchyExpandedState.CollapseAll);
        }

        private static void DrawIconButton(Rect rect, string tooltip, System.Action<Rect> drawIcon, System.Action onClick)
        {
            bool hovering = rect.Contains(Event.current.mousePosition);

            if (hovering && Event.current.type == EventType.Repaint)
            {
                Color highlight = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0.12f);
                Texture2D roundedTex = RoundedTextureProvider.Get();
                if (roundedTex != null)
                {
                    var prev = GUI.color;
                    GUI.color = highlight;
                    GUI.DrawTexture(rect, roundedTex);
                    GUI.color = prev;
                }
                else
                {
                    EditorGUI.DrawRect(rect, highlight);
                }
            }

            drawIcon(rect);
            GUI.Label(rect, new GUIContent(string.Empty, tooltip));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                onClick();
                Event.current.Use();
            }
        }

        private static Color ActionIconColor => EditorGUIUtility.isProSkin
            ? new Color(0.85f, 0.85f, 0.85f)
            : new Color(0.25f, 0.25f, 0.25f);

        private static Texture2D _dividerIconTex;
        private static Texture2D _headerIconTex;
        private static Texture2D _collapseIconTex;

        private static void DrawDividerIcon(Rect rect) => DrawActionIconTexture(rect, ref _dividerIconTex, "divider-icon");
        private static void DrawHeaderIcon(Rect rect) => DrawActionIconTexture(rect, ref _headerIconTex, "header-icon");
        private static void DrawCollapseIcon(Rect rect) => DrawActionIconTexture(rect, ref _collapseIconTex, "collapse-icon", 90f);

        /// <summary>
        /// Loads (and caches) a user-supplied icon from Editor/Resources and draws it tinted with
        /// ActionIconColor, same skin-adaptive tint the buttons used for their hand-drawn
        /// placeholder icons before these were added. Assumes a white/transparent source image -
        /// same convention as RoundedTextureProvider's texture - so the tint fully determines the
        /// visible color rather than just shading an already-colored icon.
        /// </summary>
        private static void DrawActionIconTexture(Rect rect, ref Texture2D cache, string resourceName, float rotationDegrees = 0f)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (cache == null)
                cache = Resources.Load<Texture2D>(resourceName);

            if (cache == null) return;

            const float iconInset = 3f;
            Rect iconRect = new Rect(rect.x + iconInset, rect.y + iconInset,
                rect.width - iconInset * 2f, rect.height - iconInset * 2f);

            Matrix4x4 prevMatrix = GUI.matrix;
            if (rotationDegrees != 0f)
                GUIUtility.RotateAroundPivot(rotationDegrees, iconRect.center);

            var prevColor = GUI.color;
            GUI.color = ActionIconColor;
            GUI.DrawTexture(iconRect, cache, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            GUI.matrix = prevMatrix;
        }

        private static void EnsureStyles()
        {
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

        private static void DrawChip(Rect chipRect, PinInfo pin, int index)
        {
            GameObject resolvedGo = HierarchyPins.TryResolve(pin.Id);
            bool resolved = resolvedGo != null;
            bool hovering = chipRect.Contains(Event.current.mousePosition);

            Texture2D roundedTex = RoundedTextureProvider.Get();
            Color chipColor = hovering && resolved
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

            // Reflects the object's own icon (including a custom one set via the Alt+Click icon
            // picker) same as the Hierarchy row itself - only falls back to the generic icon when
            // unresolved, since there's no live object to read a custom icon off of then.
            Texture icon = resolved
                ? EditorGUIUtility.ObjectContent(resolvedGo, resolvedGo.GetType()).image
                : EditorGUIUtility.IconContent("GameObject Icon").image;
            if (icon != null)
            {
                Rect iconRect = new Rect(chipRect.x + (ChipSize - IconSize) / 2f, chipRect.y + (ChipSize - IconSize) / 2f, IconSize, IconSize);

                var prevColor = GUI.color;
                GUI.color = resolved ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(iconRect, icon);
                GUI.color = prevColor;
            }

            string sceneName = HierarchyPins.GetSceneName(pin.Id);
            string tooltip = resolved
                ? $"{pin.DisplayName} ({sceneName})"
                : $"{pin.DisplayName} ({sceneName}) - not loaded";
            GUI.Label(chipRect, new GUIContent(string.Empty, tooltip));

            HandleChipInteraction(chipRect, pin, index, resolved);
        }

        private static void HandleChipInteraction(Rect rect, PinInfo pin, int index, bool resolved)
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
                    menu.AddItem(new GUIContent("Remove"), false, () => HierarchyPins.Remove(pin.Id));
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
                            HierarchyPins.Reorder(_pressIndex, j);
                            _pressIndex = j;
                            break;
                        }
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _pressIndex == index)
            {
                if (!_isDraggingChip && resolved)
                    NavigateTo(pin.Id);

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
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static void NavigateTo(string id)
        {
            GameObject go = HierarchyPins.TryResolve(id);
            if (go == null) return;

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static void HandleDragAndDrop(Rect rect)
        {
            Event e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!rect.Contains(e.mousePosition)) return;

            bool hasGameObject = DragAndDrop.objectReferences
                .Any(obj => obj is GameObject go && go.scene.IsValid());

            if (!hasGameObject)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            if (e.type == EventType.DragPerform)
            {
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && go.scene.IsValid())
                        HierarchyPins.Add(go);
                }
                DragAndDrop.AcceptDrag();
            }

            e.Use();
        }
    }
}
