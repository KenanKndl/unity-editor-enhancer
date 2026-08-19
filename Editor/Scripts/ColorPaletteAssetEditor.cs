using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    [CustomEditor(typeof(ColorPaletteAsset))]
    public class ColorPaletteAssetEditor : Editor
    {
        private const float SwatchSize = 28f;
        private const float Spacing = 6f;

        // Kept short on purpose: the Inspector is most often docked in the narrow default side
        // panel, and GUILayout.Toolbar clips text rather than shrinking it - longer labels like
        // "Project Window" or "Hierarchy Window" would render blank there.
        private static readonly string[] TabNames =
        {
            "Colors", "Icons", "Project", "Hierarchy", "Stats"
        };

        // Static, not per-instance: keeps the selected tab sticky across the Editor instance
        // recreations Unity does on every selection change, without needing an EditorPrefs key.
        private static int _selectedTab;

        // Read once from the installed package manifest so this label can't drift out of sync
        // with package.json on future releases the way a hardcoded string would. Resolved lazily
        // in OnEnable rather than a field initializer - Unity forbids PackageInfo lookups from a
        // ScriptableObject/Editor type initializer and throws a TypeInitializationException there.
        private static string _packageVersionLabel;

        private static List<Color> _dragList;
        private static int _dragFromIndex = -1;
        private static bool _isDragging;

        // Index into customColors currently being edited in place, or -1 when the color panel
        // holds an unsaved new color instead (e.g. right after clicking a Default preset).
        private static int _editingCustomIndex = -1;

        private GUIStyle _sectionStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _hexFieldStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _tabStyle;

        private void OnEnable()
        {
            if (_packageVersionLabel != null) return;

            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ColorPaletteAssetEditor).Assembly);
            _packageVersionLabel = info != null ? $"v{info.version} · Project Enhancements" : "Project Enhancements";
        }

        private void InitStyles()
        {
            if (_sectionStyle != null && _bannerStyle != null) return;

            _sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 0, 10)
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(2, 0, 4, 4)
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                margin = new RectOffset(2, 0, 0, 6)
            };

            _hexFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _bannerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 12)
            };

            _footerStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 8, 4)
            };

            // Wrapping instead of clipping: even with the shortened labels above, the narrow
            // default Inspector panel can still pinch a tab below its label's single-line width.
            _tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                wordWrap = true,
                fontSize = 10
            };
        }

        private static readonly Color HeartColor = new Color32(0xFA, 0x00, 0x50, 0xFF);
        private static Texture2D _heartIconTex;

        private static void DrawHeartIcon()
        {
            if (_heartIconTex == null)
                _heartIconTex = Resources.Load<Texture2D>("heart-filled");

            // Reserve the same row height the adjacent "LenzDev" label uses (not just the icon's
            // own 12px), then center the icon within it - otherwise a shorter fixed-size rect
            // sits top-aligned against a taller label and reads as sitting above the text.
            const float iconSize = 12f;
            float rowHeight = EditorStyles.miniBoldLabel.CalcHeight(GUIContent.none, 100f);
            Rect reserved = GUILayoutUtility.GetRect(iconSize, rowHeight, GUILayout.Width(iconSize), GUILayout.Height(rowHeight));
            if (_heartIconTex == null) return;

            Rect iconRect = new Rect(reserved.x, reserved.y + (reserved.height - iconSize) / 2f, iconSize, iconSize);

            var prevColor = GUI.color;
            GUI.color = HeartColor;
            GUI.DrawTexture(iconRect, _heartIconTex, ScaleMode.ScaleToFit);
            GUI.color = prevColor;
        }

        private static void DrawSeparator()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.1f)
                : new Color(0f, 0f, 0f, 0.15f));
        }

        public override void OnInspectorGUI()
        {
            var palette = (ColorPaletteAsset)target;
            InitStyles();

            // Only relevant to the Colors tab - gated so Delete doesn't accidentally fire while
            // browsing another tab (e.g. deleting text mid-edit in a field elsewhere).
            if (_selectedTab == 0 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                if (_editingCustomIndex >= 0 && _editingCustomIndex < palette.customColors.Count)
                {
                    DeleteCustomColor(palette, _editingCustomIndex);
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(_bannerStyle);

            var mainTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Unity Folder Customizer", mainTitleStyle);

            EditorGUILayout.Space(2);
            DrawSeparator();
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_packageVersionLabel, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            DrawHeartIcon();

            EditorGUILayout.LabelField("LenzDev", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, _tabStyle, GUILayout.Height(26f));
            EditorGUILayout.Space(6);

            switch (_selectedTab)
            {
                case 0: DrawColorsTab(palette); break;
                case 1: DrawIconsTab(palette); break;
                case 2: DrawProjectWindowTab(); break;
                case 3: DrawHierarchyWindowTab(); break;
                case 4: DrawFolderStatsTab(palette); break;
            }

            EditorGUILayout.LabelField("LenzDev Editor Extensions © 2026", _footerStyle);
            EditorGUILayout.Space(2);
        }

        private void DrawColorsTab(ColorPaletteAsset palette)
        {
            // The customColors list can shrink from under a stale index (e.g. after an Undo, or
            // after switching to a different palette asset that has fewer entries).
            if (_editingCustomIndex >= palette.customColors.Count)
                _editingCustomIndex = -1;

            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Default Colors", _headerStyle);
            EditorGUILayout.LabelField("Fixed presets - click one to start a new custom color from it.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            DrawColorGrid(palette.defaultColors, palette, isDefault: true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Custom Colors", _headerStyle);
            DrawColorGrid(palette.customColors, palette, isDefault: false);
            if (palette.customColors.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Click to edit in place · Drag to reorder · Right-click to delete", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(_sectionStyle);
            DrawSelectedColorPanel(palette);
            EditorGUILayout.EndVertical();
        }

        private static void DeleteCustomColor(ColorPaletteAsset palette, int index)
        {
            Undo.RecordObject(palette, "Delete Color");
            palette.customColors.RemoveAt(index);
            if (_editingCustomIndex == index) _editingCustomIndex = -1;
            else if (_editingCustomIndex > index) _editingCustomIndex--;
            EditorUtility.SetDirty(palette);
        }

        private void DrawIconsTab(ColorPaletteAsset palette)
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Your Icon Palette", _headerStyle);
            DrawIconPaletteGrid(palette);
            EditorGUILayout.HelpBox(
                "Click an icon in the categories below to add it here. Alt+Click a GameObject in " +
                "the Hierarchy to assign one of these icons to it.", MessageType.None);
            EditorGUILayout.EndVertical();

            foreach (var category in IconCatalog.Categories)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                EditorGUILayout.LabelField(category.Name, _headerStyle);
                DrawIconCatalogGrid(category.IconNames, palette);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawProjectWindowTab()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Project Window Preferences", _headerStyle);

            EditorGUI.BeginChangeCheck();
            bool currentShowLines = FolderTreeSettings.ShowLines;
            bool newShowLines = EditorGUILayout.Toggle("Folder Tree Lines", currentShowLines);
            if (EditorGUI.EndChangeCheck())
            {
                FolderTreeSettings.ShowLines = newShowLines;
                EditorApplication.RepaintProjectWindow();
            }

            EditorGUI.BeginChangeCheck();
            bool currentSingleCol = FolderTreeSettings.SingleColumnColoring;
            bool newSingleCol = EditorGUILayout.Toggle("Single Column Coloring", currentSingleCol);
            if (EditorGUI.EndChangeCheck())
            {
                FolderTreeSettings.SingleColumnColoring = newSingleCol;
                EditorApplication.RepaintProjectWindow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHierarchyWindowTab()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Hierarchy Window Preferences", _headerStyle);

            EditorGUI.BeginChangeCheck();
            bool currentSingleRow = HierarchySettings.SingleRowColoring;
            bool newSingleRow = EditorGUILayout.Toggle("Single Row Coloring", currentSingleRow);
            if (EditorGUI.EndChangeCheck())
            {
                HierarchySettings.SingleRowColoring = newSingleRow;
                HierarchyColorOverlay.ClearCache();
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUI.BeginChangeCheck();
            float currentInset = HierarchySettings.PanelLeftInset;
            float newInset = EditorGUILayout.Slider("Left Edge Offset", currentInset, 0f, 40f);
            if (EditorGUI.EndChangeCheck())
            {
                HierarchySettings.PanelLeftInset = newInset;
                EditorApplication.RepaintHierarchyWindow();
            }
            EditorGUILayout.HelpBox("Matches the color overlay's left edge to the Hierarchy panel's own background. Drag while watching the Hierarchy window.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            HierarchyDividerStyle currentDividerStyle = HierarchySettings.DividerStyle;
            var newDividerStyle = (HierarchyDividerStyle)EditorGUILayout.EnumPopup("Divider Style", currentDividerStyle);
            if (EditorGUI.EndChangeCheck())
            {
                HierarchySettings.DividerStyle = newDividerStyle;
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFolderStatsTab(ColorPaletteAsset palette)
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            EditorGUILayout.LabelField("Folder Stats Appearance", _headerStyle);

            EditorGUI.BeginChangeCheck();
            Color newBgColor = EditorGUILayout.ColorField("Background Color", palette.statsBackgroundColor);
            Color newTxtColor = EditorGUILayout.ColorField("Text Color", palette.statsTextColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(palette, "Change Stats Appearance");
                palette.statsBackgroundColor = newBgColor;
                palette.statsTextColor = newTxtColor;
                EditorUtility.SetDirty(palette);
                EditorApplication.RepaintProjectWindow();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedColorPanel(ColorPaletteAsset palette)
        {
            bool isEditingCustom = _editingCustomIndex >= 0 && _editingCustomIndex < palette.customColors.Count;
            Color currentColor = isEditingCustom ? palette.customColors[_editingCustomIndex] : palette.selectedColor;

            EditorGUILayout.LabelField(isEditingCustom ? "Editing Custom Color" : "New Color", _headerStyle);

            const float previewSize = 48f;

            EditorGUILayout.BeginHorizontal();

            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize,
                GUILayout.Width(previewSize), GUILayout.Height(previewSize));

            Texture2D tex = RoundedTextureProvider.Get();
            if (tex != null)
            {
                var prev = GUI.color;
                GUI.color = currentColor;
                GUI.DrawTexture(previewRect, tex);
                GUI.color = prev;
            }
            else
            {
                EditorGUI.DrawRect(previewRect, currentColor);
            }

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical();

            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(GUIContent.none, currentColor,
                true, true, false, GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
                ApplyColorEdit(palette, isEditingCustom, newColor);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hex", GUILayout.Width(30));
            string hex = ColorUtility.ToHtmlStringRGB(currentColor);
            EditorGUI.BeginChangeCheck();
            string newHex = EditorGUILayout.DelayedTextField(hex, _hexFieldStyle);
            if (EditorGUI.EndChangeCheck())
            {
                if (ColorUtility.TryParseHtmlString("#" + newHex, out Color parsed))
                {
                    parsed.a = currentColor.a;
                    ApplyColorEdit(palette, isEditingCustom, parsed);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            DrawColorActionButtons(palette, isEditingCustom);
        }

        private static void ApplyColorEdit(ColorPaletteAsset palette, bool isEditingCustom, Color newColor)
        {
            if (isEditingCustom)
            {
                Undo.RecordObject(palette, "Edit Custom Color");
                palette.customColors[_editingCustomIndex] = newColor;
            }
            else
            {
                Undo.RecordObject(palette, "Change Color");
            }

            // Kept in sync either way: selectedColor is documented as "the currently selected
            // color, other tools can read this" - it should mirror whatever is on screen here.
            palette.selectedColor = newColor;
            EditorUtility.SetDirty(palette);
        }

        private static void DrawColorActionButtons(ColorPaletteAsset palette, bool isEditingCustom)
        {
            EditorGUILayout.BeginHorizontal();

            if (isEditingCustom)
            {
                if (GUILayout.Button("Duplicate as New", GUILayout.Height(22)))
                {
                    Undo.RecordObject(palette, "Duplicate Color");
                    Color copy = palette.customColors[_editingCustomIndex];
                    palette.customColors.Insert(_editingCustomIndex + 1, copy);
                    _editingCustomIndex += 1;
                    EditorUtility.SetDirty(palette);
                }

                if (GUILayout.Button("Delete", GUILayout.Height(22)))
                    DeleteCustomColor(palette, _editingCustomIndex);
            }
            else
            {
                if (GUILayout.Button("Add to Custom Colors", GUILayout.Height(22)))
                {
                    Undo.RecordObject(palette, "Add Color");
                    palette.customColors.Add(palette.selectedColor);
                    _editingCustomIndex = palette.customColors.Count - 1;
                    EditorUtility.SetDirty(palette);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorGrid(List<Color> colors, ColorPaletteAsset palette, bool isDefault)
        {
            if (colors.Count == 0)
            {
                EditorGUILayout.HelpBox("No colors available.", MessageType.None);
                return;
            }

            float totalWidth = EditorGUIUtility.currentViewWidth - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (SwatchSize + Spacing)));
            int rows = Mathf.CeilToInt((float)colors.Count / columns);

            EditorGUILayout.BeginVertical();
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    if (index >= colors.Count)
                    {
                        GUILayout.Space(SwatchSize + Spacing);
                    }
                    else
                    {
                        DrawSwatch(colors, index, palette, isDefault);
                        GUILayout.Space(Spacing);
                    }
                    index++;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(Spacing);
            }
            EditorGUILayout.EndVertical();

            if (Event.current.type != EventType.Layout)
            {
                Rect gridRect = GUILayoutUtility.GetLastRect();
                HandleReordering(gridRect, colors, columns, palette);
            }
        }

        private void HandleReordering(Rect gridRect, List<Color> colors, int columns, ColorPaletteAsset palette)
        {
            if (!_isDragging || _dragList != colors)
                return;

            if (Event.current.type == EventType.MouseDrag)
            {
                Vector2 local = Event.current.mousePosition - gridRect.position;
                int col = Mathf.Clamp(Mathf.FloorToInt(local.x / (SwatchSize + Spacing)), 0, columns - 1);
                int row = Mathf.Max(0, Mathf.FloorToInt(local.y / (SwatchSize + Spacing)));
                int targetIndex = Mathf.Clamp(row * columns + col, 0, colors.Count - 1);

                if (targetIndex != _dragFromIndex)
                {
                    Undo.RecordObject(palette, "Reorder Colors");
                    bool wasEditingDragged = colors == palette.customColors && _editingCustomIndex == _dragFromIndex;
                    Color moved = colors[_dragFromIndex];
                    colors.RemoveAt(_dragFromIndex);
                    colors.Insert(targetIndex, moved);
                    if (wasEditingDragged) _editingCustomIndex = targetIndex;
                    _dragFromIndex = targetIndex;
                    EditorUtility.SetDirty(palette);
                    Event.current.Use();
                    GUI.changed = true;
                    Repaint();
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                _isDragging = false;
                _dragList = null;
                _dragFromIndex = -1;
                Event.current.Use();
            }
        }

        private void DrawSwatch(List<Color> colors, int index, ColorPaletteAsset palette, bool isDefault)
        {
            Color color = colors[index];
            Rect rect = GUILayoutUtility.GetRect(SwatchSize, SwatchSize,
                GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));

            bool isSelected = isDefault ? palette.selectedColor == color : index == _editingCustomIndex;
            Texture2D roundedTex = RoundedTextureProvider.Get();

            if (roundedTex == null)
            {
                EditorGUI.DrawRect(rect, color);
            }
            else
            {
                if (isSelected)
                {
                    // A thin, slightly-translucent ring instead of a full-opacity 2px one - reads
                    // as a crisp selection indicator rather than a bold frame around the swatch.
                    Rect outlineRect = new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f);
                    var prevColorOutline = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.85f);
                    GUI.DrawTexture(outlineRect, roundedTex);
                    GUI.color = prevColorOutline;
                }

                var prevColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(rect, roundedTex);
                GUI.color = prevColor;
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    Undo.RecordObject(palette, "Select Color");
                    palette.selectedColor = color;
                    _editingCustomIndex = isDefault ? -1 : index;
                    EditorUtility.SetDirty(palette);

                    _isDragging = true;
                    _dragList = colors;
                    _dragFromIndex = index;

                    Event.current.Use();
                }
                else if (Event.current.button == 1 && !isDefault)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Delete"), false, () => DeleteCustomColor(palette, index));
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }
        }

        private void DrawIconPaletteGrid(ColorPaletteAsset palette)
        {
            if (palette.customIcons.Count == 0)
            {
                EditorGUILayout.HelpBox("No icons added yet - click any icon in the categories below.", MessageType.None);
                return;
            }

            float totalWidth = EditorGUIUtility.currentViewWidth - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (SwatchSize + Spacing)));
            int rows = Mathf.CeilToInt((float)palette.customIcons.Count / columns);

            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    if (index >= palette.customIcons.Count)
                    {
                        GUILayout.Space(SwatchSize + Spacing);
                    }
                    else
                    {
                        int capturedIndex = index;
                        string iconName = palette.customIcons[capturedIndex];
                        DrawIconSwatchButton(iconName, onRightClick: () =>
                        {
                            Undo.RecordObject(palette, "Remove Icon");
                            palette.customIcons.RemoveAt(capturedIndex);
                            EditorUtility.SetDirty(palette);
                        });
                        GUILayout.Space(Spacing);
                    }
                    index++;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(Spacing);
            }

            EditorGUILayout.LabelField("Right-click an icon to remove it", EditorStyles.miniLabel);
        }

        private void DrawIconCatalogGrid(string[] iconNames, ColorPaletteAsset palette)
        {
            float totalWidth = EditorGUIUtility.currentViewWidth - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(totalWidth / (SwatchSize + Spacing)));
            int rows = Mathf.CeilToInt((float)iconNames.Length / columns);

            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    if (index >= iconNames.Length)
                    {
                        GUILayout.Space(SwatchSize + Spacing);
                    }
                    else
                    {
                        string iconName = iconNames[index];
                        DrawIconSwatchButton(iconName, onLeftClick: () =>
                        {
                            if (palette.customIcons.Contains(iconName)) return;
                            Undo.RecordObject(palette, "Add Icon");
                            palette.customIcons.Add(iconName);
                            EditorUtility.SetDirty(palette);
                        });
                        GUILayout.Space(Spacing);
                    }
                    index++;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(Spacing);
            }
        }

        private void DrawIconSwatchButton(string iconName, System.Action onLeftClick = null, System.Action onRightClick = null)
        {
            Rect rect = GUILayoutUtility.GetRect(SwatchSize, SwatchSize,
                GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.05f));

            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon != null)
            {
                const float iconSize = 18f;
                Rect iconRect = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            GUI.Label(rect, new GUIContent(string.Empty, iconName));

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0 && onLeftClick != null)
                {
                    onLeftClick();
                    Event.current.Use();
                }
                else if (Event.current.button == 1 && onRightClick != null)
                {
                    onRightClick();
                    Event.current.Use();
                }
            }
        }
    }
}
