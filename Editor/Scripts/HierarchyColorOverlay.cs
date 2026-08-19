using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    [InitializeOnLoad]
    public static class HierarchyColorOverlay
    {
        private const float CheckboxSize = 14f;
        private const float CheckboxLeftGap = 2f;  // gap from the color band's start to the checkbox
        private const float CheckboxRightGap = 4f; // gap from the checkbox to the icon that follows it
        private const float FoldoutWidth = 14f;
        private const float EyeIconSize = 16f;
        private const float EyeIconGap = 2f;       // gap between the eye icon and the foldout arrow

        private static readonly Dictionary<int, (bool hasColor, Color color)> _resolvedCache = new();

        // Unity's real default (unselected) row label color, confirmed live via MCP
        // execute_code against EditorStyles.label.normal.textColor. FolderColorOverlay.
        // DefaultTextColor (255,255,255 on pro skin) is noticeably brighter than this and was
        // only ever validated against already-colored rows; once every row started going through
        // this same draw path (for the active checkbox), plain uncolored objects picked up that
        // same too-bright white for the first time. EditorStyles.label already tracks the active
        // skin on its own, so no light/dark ternary is needed here.
        private static Color DefaultRowTextColor => EditorStyles.label.normal.textColor;

        private static readonly Color DefaultOrganizerAccent = new Color(0.55f, 0.55f, 0.55f);

        private static GUIStyle _labelStyle;
        private static GUIStyle _organizerLabelStyle;

        static HierarchyColorOverlay()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
            EditorApplication.hierarchyChanged += ClearCache;
        }

        public static void ClearCache() => _resolvedCache.Clear();

        private static void OnGUI(int instanceID, Rect selectionRect)
        {
            GameObject go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            if (go == null) return;

            // Checked ahead of the normal color path (not as a separately-subscribed overlay):
            // subscriber call order across static constructors isn't something to rely on, and
            // an organizer row must never fall through to the normal icon+label DrawRow.
            HierarchyOrganizerMarker organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer != null)
            {
                DrawOrganizerRow(go, instanceID, selectionRect, organizer);
                return;
            }

            // Every row now goes through DrawRow, colored or not: the active checkbox needs
            // icon+label shifted right on EVERY row, so Unity's own (unshifted) drawing can no
            // longer be left showing through for uncolored objects like it used to be.
            bool hasColor = TryResolveColor(go, out Color color);
            DrawRow(go, instanceID, selectionRect, hasColor ? color : (Color?)null);
        }

        private static bool _hoverColorReflectionFailed;
        private static FieldInfo _hoveredBackgroundColorField;

        /// <summary>
        /// Unity's own row hover background, read via reflection off the exact internal field the
        /// Hierarchy's GameObject rows use (UnityEditor.GameObjectTreeViewGUI+GameObjectStyles.
        /// hoveredBackgroundColor - confirmed live via MCP execute_code, not guessed: it's a solid
        /// opaque color that REPLACES the row background on hover, not a translucent tint drawn
        /// on top). Our full-row redraw (needed to hide Unity's unshifted icon/label before we
        /// draw our own checkbox-shifted copies) would otherwise paint over the real hover state
        /// entirely, so this is read fresh and used as bgColor itself while hovering. Best effort:
        /// falls back to the plain default background if a future Unity version renames/removes
        /// the field.
        /// </summary>
        private static Color GetHoverBackgroundColor(Color fallback)
        {
            if (_hoverColorReflectionFailed) return fallback;

            try
            {
                if (_hoveredBackgroundColorField == null)
                {
                    Type stylesType = typeof(Editor).Assembly.GetType("UnityEditor.GameObjectTreeViewGUI+GameObjectStyles");
                    _hoveredBackgroundColorField = stylesType?.GetField("hoveredBackgroundColor",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (_hoveredBackgroundColorField == null)
                    {
                        _hoverColorReflectionFailed = true;
                        return fallback;
                    }
                }

                return (Color)_hoveredBackgroundColorField.GetValue(null);
            }
            catch
            {
                _hoverColorReflectionFailed = true;
                return fallback;
            }
        }

        /// <summary>
        /// Draws the row's own active/inactive checkbox - the same GameObject.activeSelf toggle
        /// as the Inspector's top-left checkbox, including the same cascading-to-children
        /// behavior, since that's simply how Unity's activeInHierarchy already works (no manual
        /// propagation needed here). Only actually drawn while hovered or selected (hover-reveal,
        /// same idea as Unity's own hover-only icons) - otherwise nothing is drawn there at all.
        ///
        /// Positioned just right of the color band's fixed start (leftMargin), not relative to
        /// this row's own indent depth - same "ignore depth" idea as the color band itself, so
        /// checkboxes form one vertical column. Deliberately does NOT influence where the
        /// foldout/icon/label get drawn - those stay exactly where Unity's own layout puts them
        /// (rect.x), left untouched on purpose. Any visual overlap between this fixed-position
        /// checkbox and a shallow row's own foldout/icon is an accepted trade-off of keeping
        /// everything else pixel-identical to Unity's default.
        /// </summary>
        private static void DrawActiveCheckbox(GameObject go, Rect rect, bool visible)
        {
            if (!visible) return;

            float leftMargin = Mathf.Min(HierarchySettings.PanelLeftInset, rect.x);
            float checkboxX = leftMargin + CheckboxLeftGap;
            Rect checkboxRect = new Rect(checkboxX, rect.y + (rect.height - CheckboxSize) / 2f, CheckboxSize, CheckboxSize);

            EditorGUI.BeginChangeCheck();
            bool newActive = EditorGUI.Toggle(checkboxRect, go.activeSelf);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(go, "Toggle Active");
                go.SetActive(newActive);
                if (!Application.isPlaying && go.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }

        private static bool _visibilityIconReflectionFailed;
        private static MethodInfo _drawVisibilityIconMethod;

        /// <summary>
        /// Restores Unity's own native scene-visibility ("eye") icon, invoked directly via
        /// reflection (UnityEditor.SceneVisibilityHierarchyGUI.DrawGameObjectItemVisibility) so
        /// its look, hidden-state persistence, and click-to-toggle behavior are exactly Unity's
        /// own - not reimplemented. Our full-row background redraw (needed for the checkbox)
        /// paints over Unity's own copy before we get a chance to see it, same underlying reason
        /// GetHoverBackgroundColor exists. Placed left of the foldout arrow's own 14px slot, at a
        /// position fixed to the row's margin rather than its indent depth.
        /// </summary>
        private static void DrawSceneVisibilityIcon(GameObject go, Rect rect, bool isItemHovered)
        {
            if (_visibilityIconReflectionFailed) return;

            try
            {
                if (_drawVisibilityIconMethod == null)
                {
                    Type visibilityType = typeof(Editor).Assembly.GetType("UnityEditor.SceneVisibilityHierarchyGUI");
                    _drawVisibilityIconMethod = visibilityType?.GetMethod("DrawGameObjectItemVisibility",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (_drawVisibilityIconMethod == null)
                    {
                        _visibilityIconReflectionFailed = true;
                        return;
                    }
                }

                float leftMargin = Mathf.Min(HierarchySettings.PanelLeftInset, rect.x);
                float iconX = leftMargin - FoldoutWidth - EyeIconGap - EyeIconSize;
                Rect iconRect = new Rect(iconX, rect.y + (rect.height - EyeIconSize) / 2f, EyeIconSize, EyeIconSize);

                bool isIconHovered = iconRect.Contains(Event.current.mousePosition);
                _drawVisibilityIconMethod.Invoke(null, new object[] { iconRect, go, isItemHovered, isIconHovered });
            }
            catch
            {
                _visibilityIconReflectionFailed = true;
            }
        }

        private static void DrawOrganizerRow(GameObject go, int instanceID, Rect rect, HierarchyOrganizerMarker organizer)
        {
            HierarchyOrganizerMarker.OrganizerKind kind = organizer.kind;
            bool isSelected = Selection.entityIds.Contains(instanceID);

            // Same edge-to-edge full-bleed rect as DrawRow, so a divider/header spans the whole
            // window width regardless of its indent depth.
            float leftEdge = Mathf.Min(HierarchySettings.PanelLeftInset, rect.x);
            float rightEdge = Mathf.Max(rect.xMax, EditorGUIUtility.currentViewWidth);
            Rect fullRect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);
            bool isHovering = fullRect.Contains(Event.current.mousePosition);

            Color bgColor = isSelected
                ? FolderColorOverlay.DefaultEditorSelectedColorMainList
                : (isHovering
                    ? GetHoverBackgroundColor(FolderColorOverlay.DefaultEditorColorMainList)
                    : FolderColorOverlay.DefaultEditorColorMainList);

            EditorGUI.DrawRect(fullRect, bgColor);

            // Reuses the existing Alt+click color picker / HierarchyColorMarker pipeline for the
            // accent color, rather than introducing a second per-object color field.
            if (!HierarchyMetaData.TryGetColor(go, out Color accent))
                accent = DefaultOrganizerAccent;

            if (kind == HierarchyOrganizerMarker.OrganizerKind.Header)
            {
                Color bandColor = accent;
                bandColor.a = 0.28f;
                EditorGUI.DrawRect(fullRect, bandColor);

                DrawSceneVisibilityIcon(go, rect, isHovering);
                DrawActiveCheckbox(go, rect, isSelected || isHovering);

                if (_organizerLabelStyle == null)
                {
                    _organizerLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                // Every style property is re-applied every draw (not just on first creation),
                // since they differ per-header rather than being a single shared constant.
                _organizerLabelStyle.fontStyle = organizer.textStyle;
                _organizerLabelStyle.fontSize = organizer.fontSize;
                _organizerLabelStyle.alignment = organizer.textAlign switch
                {
                    HierarchyOrganizerMarker.TextAlign.Left => TextAnchor.MiddleLeft,
                    HierarchyOrganizerMarker.TextAlign.Right => TextAnchor.MiddleRight,
                    _ => TextAnchor.MiddleCenter
                };
                _organizerLabelStyle.normal.textColor = organizer.hasCustomTextColor
                    ? organizer.textColor
                    : (isSelected ? Color.white : DefaultRowTextColor);

                // Small inset so left/right-aligned text doesn't touch the row's edge.
                Rect textRect = new Rect(fullRect.x + 8f, fullRect.y, fullRect.width - 16f, fullRect.height);

                // The GameObject's own name IS the header text - renaming it (F2, same as any
                // GameObject) is the only editing UI a header needs.
                EditorGUI.LabelField(textRect, go.name, _organizerLabelStyle);
            }
            else
            {
                if (HierarchySettings.DividerStyle == HierarchyDividerStyle.Band)
                {
                    Color bandColor = accent;
                    bandColor.a = 0.45f;
                    EditorGUI.DrawRect(fullRect, bandColor);
                }
                else
                {
                    const float lineHeight = 2f;
                    Rect lineRect = new Rect(fullRect.x + 6f, rect.y + (rect.height - lineHeight) / 2f,
                        fullRect.width - 12f, lineHeight);
                    EditorGUI.DrawRect(lineRect, accent);
                }

                // No text to reposition for a divider, but the icon/checkbox still need drawing -
                // they float on top of the line/band, same as they float on top of a colored row.
                DrawSceneVisibilityIcon(go, rect, isHovering);
                DrawActiveCheckbox(go, rect, isSelected || isHovering);
            }

            if (go.transform.childCount > 0 && Event.current.type == EventType.Repaint)
            {
                bool isExpanded = HierarchyExpandedState.IsExpanded(instanceID);
                Rect foldoutRect = new Rect(rect.x - 14f, rect.y, 14f, rect.height);
                EditorStyles.foldout.Draw(foldoutRect, false, false, isExpanded, false);
            }
        }

        private static bool TryResolveColor(GameObject go, out Color color)
        {
            int id = go.GetInstanceID();
            if (_resolvedCache.TryGetValue(id, out var cached))
            {
                color = cached.color;
                return cached.hasColor;
            }

            bool hasColor = HierarchyMetaData.TryGetColor(go, out color);

            if (!hasColor && !HierarchySettings.SingleRowColoring)
            {
                Transform parent = go.transform.parent;
                while (parent != null)
                {
                    if (HierarchyMetaData.TryGetColor(parent.gameObject, out Color parentColor))
                    {
                        color = parentColor;
                        hasColor = true;
                        break;
                    }
                    parent = parent.parent;
                }
            }

            _resolvedCache[id] = (hasColor, color);
            return hasColor;
        }

        private static void DrawRow(GameObject go, int instanceID, Rect rect, Color? tintColor)
        {
            bool isSelected = Selection.entityIds.Contains(instanceID);

            // Cover the row edge-to-edge (the raw rect stops short on both sides: Unity reserves
            // a strip on the left for the indent/foldout gutter and on the right for hover-only
            // icons), so the color reads as one unified band instead of an inset pill - same
            // reach for every row regardless of depth or whether it has children, so a colored
            // parent's band lines up exactly with its colored children's.
            float leftEdge = Mathf.Min(HierarchySettings.PanelLeftInset, rect.x);
            float rightEdge = Mathf.Max(rect.xMax, EditorGUIUtility.currentViewWidth);
            Rect fullRect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);
            bool isHovering = fullRect.Contains(Event.current.mousePosition);

            Color bgColor = isSelected
                ? FolderColorOverlay.DefaultEditorSelectedColorMainList
                : (isHovering
                    ? GetHoverBackgroundColor(FolderColorOverlay.DefaultEditorColorMainList)
                    : FolderColorOverlay.DefaultEditorColorMainList);

            EditorGUI.DrawRect(fullRect, bgColor);

            if (tintColor.HasValue)
            {
                CachedTextureData tex = GradientTextureCache.GetOrCreate(tintColor.Value, FolderColorOverlay.DefaultEditorColorNoAlphaMainList);
                if (tex.ForwardTexture != null)
                    GUI.DrawTexture(fullRect, tex.ForwardTexture);
            }

            DrawSceneVisibilityIcon(go, rect, isHovering);
            DrawActiveCheckbox(go, rect, isSelected || isHovering);

            bool isActive = go.activeInHierarchy;

            Texture icon = EditorGUIUtility.ObjectContent(go, go.GetType()).image;
            float iconX = rect.x;
            float labelX = iconX + 16f + 2f;

            if (icon != null)
            {
                var prevColor = GUI.color;
                GUI.color = isActive ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(new Rect(iconX, rect.y, 16f, 16f), icon);
                GUI.color = prevColor;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(2, 0, 0, 0)
                };
            }

            Color textColor = isSelected ? Color.white : DefaultRowTextColor;
            if (!isActive) textColor.a = 0.6f;
            _labelStyle.normal.textColor = textColor;

            // rect.xMax, not rect.width: rect.width is the row's own span, not the distance from
            // labelX (an absolute coordinate) to the row's right edge. Using rect.width here goes
            // negative as soon as the panel narrows past labelX, well before it's actually too
            // narrow to show anything - LabelField silently draws nothing for a negative-width
            // rect, leaving the opaque background looking like it swallowed the text.
            float labelWidth = Mathf.Max(0f, rect.xMax - labelX);
            EditorGUI.LabelField(new Rect(labelX, rect.y, labelWidth, rect.height), go.name, _labelStyle);

            if (go.transform.childCount > 0 && Event.current.type == EventType.Repaint)
            {
                bool isExpanded = HierarchyExpandedState.IsExpanded(instanceID);
                Rect foldoutRect = new Rect(rect.x - 14f, rect.y, 14f, rect.height);
                EditorStyles.foldout.Draw(foldoutRect, false, false, isExpanded, false);
            }
        }
    }
}
