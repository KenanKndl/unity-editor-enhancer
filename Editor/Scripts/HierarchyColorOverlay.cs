using System;
using System.Collections.Generic;
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

        private static Color DefaultRowTextColor => EditorStyles.label.normal.textColor;

        private static readonly Color DefaultOrganizerAccent = new Color(0.55f, 0.55f, 0.55f);

        private static GUIStyle _labelStyle;
        private static GUIStyle _organizerLabelStyle;

        /// <summary>
        /// Do not reuse Unity's internal "TV Line" GUIStyle here even though its metrics match -
        /// it produces a stray background artifact on hover/selected rows. Use a plain
        /// EditorStyles.label copy with the two corrected fields instead.
        /// </summary>
        private static void EnsureLabelStyle()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.UpperLeft
            };
        }

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

            HierarchyOrganizerMarker organizer = go.GetComponent<HierarchyOrganizerMarker>();
            if (organizer != null)
            {
                DrawOrganizerRow(go, instanceID, selectionRect, organizer);
                return;
            }

            bool hasColor = TryResolveColor(go, out Color color);
            DrawRow(go, instanceID, selectionRect, hasColor ? color : (Color?)null);
        }

        private static bool _hoverColorReflectionFailed;
        private static FieldInfo _hoveredBackgroundColorField;

        /// <summary>
        /// Reads Unity's native row hover background via reflection, since the full-row redraw
        /// below would otherwise paint over it. Falls back to the default background color if a
        /// future Unity version renames or removes the field.
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
        /// Hover-reveal active/inactive checkbox, fixed to the panel's left margin rather than the
        /// row's indent depth so checkboxes form a single vertical column.
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
        /// Restores Unity's native scene-visibility icon via reflection, since the full-row redraw
        /// below would otherwise paint over it.
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
            bool isSelected = SelectionCache.IsObjectSelected(instanceID);

            Rect fullRect = GetFullRowRect(rect, out bool isHovering);
            Color bgColor = ResolveRowBackground(isSelected, isHovering);

            EditorGUI.DrawRect(fullRect, bgColor);

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

                Rect textRect = new Rect(fullRect.x + 8f, fullRect.y, fullRect.width - 16f, fullRect.height);
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

        private static Rect GetFullRowRect(Rect rect, out bool isHovering)
        {
            float leftEdge = Mathf.Min(HierarchySettings.PanelLeftInset, rect.x);
            float rightEdge = Mathf.Max(rect.xMax, EditorGUIUtility.currentViewWidth);
            Rect fullRect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);
            isHovering = fullRect.Contains(Event.current.mousePosition);
            return fullRect;
        }

        private static Color ResolveRowBackground(bool isSelected, bool isHovering)
        {
            return isSelected
                ? FolderColorOverlay.DefaultEditorSelectedColorMainList
                : (isHovering
                    ? GetHoverBackgroundColor(FolderColorOverlay.DefaultEditorColorMainList)
                    : FolderColorOverlay.DefaultEditorColorMainList);
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
            bool isSelected = SelectionCache.IsObjectSelected(instanceID);
            Rect fullRect = GetFullRowRect(rect, out bool isHovering);

            // A plain row (uncolored, unselected, unhovered) needs no redraw - Unity's own
            // background/icon/label are already correct.
            bool needsFullRedraw = tintColor.HasValue || isSelected || isHovering;

            if (needsFullRedraw)
            {
                Color bgColor = ResolveRowBackground(isSelected, isHovering);
                EditorGUI.DrawRect(fullRect, bgColor);

                if (tintColor.HasValue)
                {
                    CachedTextureData tex = GradientTextureCache.GetOrCreate(tintColor.Value, FolderColorOverlay.DefaultEditorColorNoAlphaMainList);
                    if (tex.ForwardTexture != null)
                        GUI.DrawTexture(fullRect, tex.ForwardTexture);
                }

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

                EnsureLabelStyle();

                Color textColor = isSelected ? Color.white : DefaultRowTextColor;
                if (!isActive) textColor.a = 0.6f;
                _labelStyle.normal.textColor = textColor;

                // Use rect.xMax, not rect.width - GUI.Label silently draws nothing for a
                // negative-width rect, which rect.width alone can go to before the panel is
                // actually too narrow to show anything.
                float labelWidth = Mathf.Max(0f, rect.xMax - labelX);
                GUI.Label(new Rect(labelX, rect.y, labelWidth, rect.height), go.name, _labelStyle);
            }

            DrawSceneVisibilityIcon(go, rect, isHovering);
            DrawActiveCheckbox(go, rect, isSelected || isHovering);

            if (go.transform.childCount > 0 && Event.current.type == EventType.Repaint)
            {
                bool isExpanded = HierarchyExpandedState.IsExpanded(instanceID);
                Rect foldoutRect = new Rect(rect.x - 14f, rect.y, 14f, rect.height);
                EditorStyles.foldout.Draw(foldoutRect, false, false, isExpanded, false);
            }
        }
    }
}
