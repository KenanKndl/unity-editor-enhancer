using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    [CustomEditor(typeof(ColorPaletteAsset))]
    public class ColorPaletteAssetEditor : Editor
    {
        private const float SwatchSize = 30f;
        private const float Spacing = 8f;

        // Sharp corners everywhere except the round color-swatch preview (RoundedTextureProvider).
        private const float CardRadius = 0f;
        private const float TabBarHeight = 28f;

        // Brand color - used sparingly (banner underline and heart icon only).
        private static readonly Color AccentColor = new Color32(0xFA, 0x00, 0x50, 0xFF);

        // Fixed dark background, matches ColorPickerPopup and SceneQuickSwitchPopup.
        private static readonly Color CardBackgroundColor = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color TabTrackFill = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color TabSelectedFill = new Color(1f, 1f, 1f, 0.13f);

        // Explicit text colors for the fixed-dark cards above (not skin-adaptive).
        private static readonly Color LightTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color MutedTextColor = new Color(0.92f, 0.92f, 0.92f, 0.75f);
        private static readonly Color TabUnselectedText = new Color(0.92f, 0.92f, 0.92f, 0.55f);

        // Kept short - GUILayout.Toolbar clips rather than shrinks text in a narrow docked Inspector.
        private static readonly string[] TabNames =
        {
            "Colors", "Icons", "Project", "Hierarchy", "Stats"
        };

        // Static so the selected tab survives Editor instance recreation on selection change.
        private static int _selectedTab;

        // Resolved lazily in OnEnable - PackageInfo lookups throw from a static field initializer.
        private static string _packageVersionLabel;

        private static List<Color> _dragList;
        private static int _dragFromIndex = -1;
        private static bool _isDragging;

        // -1 when the color panel holds an unsaved new color instead of editing an existing entry.
        private static int _editingCustomIndex = -1;

        // Backgrounds/borders are painted separately by EditorGuiKit; these only carry
        // padding/margin/typography.
        private GUIStyle _sectionStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _hexFieldStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _tabLabelStyle;
        private GUIStyle _tabLabelSelectedStyle;
        private GUIStyle _mainTitleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _versionLabelStyle;
        private GUIStyle _brandLabelStyle;
        private GUIStyle _fieldLabelStyle;
        private GUIStyle _noticeStyle;

        private void OnEnable()
        {
            if (_packageVersionLabel == null)
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ColorPaletteAssetEditor).Assembly);
                _packageVersionLabel = info != null ? $"v{info.version} · Project Enhancements" : "Project Enhancements";
            }

            // Must run on every instance - Editor is recreated per selection change.
            PackageUpdateChecker.EnsureInstalledVersionLoaded();
            PackageUpdateChecker.Changed += Repaint;
        }

        private void OnDisable()
        {
            PackageUpdateChecker.Changed -= Repaint;
        }

        private void InitStyles()
        {
            // Every field must be checked - a domain reload can preserve some already-initialized
            // fields while leaving newly added ones null.
            if (_sectionStyle != null && _headerStyle != null && _subtitleStyle != null &&
                _hexFieldStyle != null && _bannerStyle != null && _footerStyle != null &&
                _tabLabelStyle != null && _tabLabelSelectedStyle != null && _mainTitleStyle != null &&
                _versionLabelStyle != null && _brandLabelStyle != null && _fieldLabelStyle != null &&
                _noticeStyle != null)
                return;

            _sectionStyle = new GUIStyle
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 0, 10)
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 0, 2),
                normal = { textColor = LightTextColor }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                margin = new RectOffset(0, 0, 2, 6),
                normal = { textColor = MutedTextColor }
            };

            _hexFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _bannerStyle = new GUIStyle
            {
                padding = new RectOffset(14, 14, 12, 12),
                margin = new RectOffset(0, 0, 0, 12)
            };

            _footerStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 8, 4),
                normal = { textColor = MutedTextColor }
            };

            _tabLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            _tabLabelSelectedStyle = new GUIStyle(_tabLabelStyle);

            _mainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = LightTextColor }
            };

            _versionLabelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = MutedTextColor } };
            _brandLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = LightTextColor } };
            _fieldLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = LightTextColor } };
            _noticeStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, normal = { textColor = LightTextColor } };
        }

        private static Texture2D _heartIconTex;

        private static void DrawHeartIcon()
        {
            if (_heartIconTex == null)
                _heartIconTex = Resources.Load<Texture2D>("heart-filled");

            const float iconSize = 12f;
            float rowHeight = EditorStyles.miniBoldLabel.CalcHeight(GUIContent.none, 100f);
            Rect reserved = GUILayoutUtility.GetRect(iconSize, rowHeight, GUILayout.Width(iconSize), GUILayout.Height(rowHeight));
            if (_heartIconTex == null) return;

            // Nudged down slightly to align with the adjacent label's glyph baseline.
            Rect iconRect = new Rect(reserved.x, reserved.y + (reserved.height - iconSize) / 2f + 2f, iconSize, iconSize);

            var prevColor = GUI.color;
            GUI.color = AccentColor;
            GUI.DrawTexture(iconRect, _heartIconTex, ScaleMode.ScaleToFit);
            GUI.color = prevColor;
        }

        private static void DrawSeparator(Color color)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(r, color);
        }

        private static Color NeutralSeparatorColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.1f)
            : new Color(0f, 0f, 0f, 0.15f);

        public override void OnInspectorGUI()
        {
            var palette = (ColorPaletteAsset)target;
            InitStyles();

            // Only relevant to the Colors tab - avoids stealing Delete from another tab's field.
            if (_selectedTab == 0 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                if (_editingCustomIndex >= 0 && _editingCustomIndex < palette.customColors.Count)
                {
                    DeleteCustomColor(palette, _editingCustomIndex);
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(4);

            EditorGuiKit.BeginCard(_bannerStyle, CardBackgroundColor, CardBorder, CardRadius);

            EditorGUILayout.LabelField("Unity Editor Enhancer", _mainTitleStyle);

            EditorGUILayout.Space(2);
            DrawSeparator(new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f));
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_packageVersionLabel, _versionLabelStyle);

            GUILayout.FlexibleSpace();

            DrawHeartIcon();

            EditorGUILayout.LabelField("LenzDev", _brandLabelStyle, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            DrawUpdateCheckRow();

            EditorGUILayout.EndVertical();

            DrawActivePaletteStatus(palette);

            EditorGUILayout.Space(2);
            DrawTabBar();
            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0: DrawColorsTab(palette); break;
                case 1: DrawIconsTab(palette); break;
                case 2: DrawProjectWindowTab(); break;
                case 3: DrawHierarchyWindowTab(); break;
                case 4: DrawFolderStatsTab(palette); break;
            }

            EditorGUILayout.Space(4);
            DrawSeparator(NeutralSeparatorColor);
            EditorGUILayout.LabelField("LenzDev Editor Extensions © 2026", _footerStyle);
            EditorGUILayout.Space(2);
        }

        /// <summary>Update-check status/action row embedded in the banner, visible regardless of the selected tab.</summary>
        private void DrawUpdateCheckRow()
        {
            bool busy = PackageUpdateChecker.IsBusy;
            bool updateAvailable = PackageUpdateChecker.UpdateAvailable;

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(busy))
            {
                if (updateAvailable)
                {
                    if (GUILayout.Button($"Update to {PackageUpdateChecker.LatestVersion}", GUILayout.Height(18)))
                        PackageUpdateChecker.StartUpdate();
                }
                else if (GUILayout.Button("Check for Updates", GUILayout.Height(18)))
                {
                    PackageUpdateChecker.StartCheck();
                }
            }

            GUILayout.FlexibleSpace();

            string statusText;
            if (busy)
                statusText = PackageUpdateChecker.IsChecking ? "Checking for updates..." : "Updating...";
            else if (updateAvailable)
                statusText = $"v{PackageUpdateChecker.LatestVersion} available";
            else if (!string.IsNullOrEmpty(PackageUpdateChecker.LatestVersion))
                statusText = "Up to date";
            else
                statusText = string.Empty;

            EditorGUILayout.LabelField(statusText, updateAvailable ? _fieldLabelStyle : _versionLabelStyle);

            EditorGUILayout.EndHorizontal();

            if (!busy && !string.IsNullOrEmpty(PackageUpdateChecker.StatusMessage))
                EditorGUILayout.LabelField(PackageUpdateChecker.StatusMessage, _subtitleStyle);

            EditorGUILayout.Space(6);
            DrawBugReportRow();
        }

        /// <summary>Opens the bug/feedback report window (see BugReportWindow).</summary>
        private static void DrawBugReportRow()
        {
            if (GUILayout.Button("Report a Bug / Send Feedback", GUILayout.Height(20)))
                BugReportWindow.Open();
        }

        /// <summary>Custom segmented tab bar - click-driven only, no hover state.</summary>
        private void DrawTabBar()
        {
            Rect barRect = GUILayoutUtility.GetRect(0, TabBarHeight, GUILayout.ExpandWidth(true));
            EditorGuiKit.DrawRoundedRect(barRect, TabTrackFill, CardRadius);

            _tabLabelStyle.normal.textColor = TabUnselectedText;
            _tabLabelSelectedStyle.normal.textColor = LightTextColor;

            float tabWidth = barRect.width / TabNames.Length;
            const float pillInset = 2f;

            for (int i = 0; i < TabNames.Length; i++)
            {
                Rect tabRect = new Rect(barRect.x + tabWidth * i, barRect.y, tabWidth, barRect.height);
                bool selected = _selectedTab == i;

                if (selected)
                {
                    Rect highlightRect = new Rect(tabRect.x + pillInset, tabRect.y + pillInset,
                        tabRect.width - pillInset * 2f, tabRect.height - pillInset * 2f);
                    EditorGuiKit.DrawRoundedRect(highlightRect, TabSelectedFill, 0f);
                }

                GUI.Label(tabRect, TabNames[i], selected ? _tabLabelSelectedStyle : _tabLabelStyle);

                if (!selected && Event.current.type == EventType.MouseDown && tabRect.Contains(Event.current.mousePosition))
                {
                    _selectedTab = i;
                    Event.current.Use();
                }
            }
        }

        /// <summary>Shared section header: bold title plus an optional muted subtitle.</summary>
        private void DrawSectionHeader(string title, string subtitle = null)
        {
            Rect headerRect = GUILayoutUtility.GetRect(0, 16f, GUILayout.ExpandWidth(true));
            GUI.Label(headerRect, title, _headerStyle);

            if (!string.IsNullOrEmpty(subtitle))
            {
                GUILayout.Space(-2f);
                EditorGUILayout.LabelField(subtitle, _subtitleStyle);
            }

            GUILayout.Space(6f);
        }

        /// <summary>Shown only when multiple ColorPaletteAssets exist in the project.</summary>
        private void DrawActivePaletteStatus(ColorPaletteAsset palette)
        {
            if (!ColorPaletteSettings.HasAmbiguity)
                return;

            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);

            bool isActive = ColorPaletteSettings.IsActive(palette);
            EditorGUILayout.LabelField(isActive
                ? "Multiple Color Palette assets exist in this project. This is the active one - " +
                  "the Project/Hierarchy overlays read from it."
                : "Multiple Color Palette assets exist in this project, and another one is active. " +
                  "Overlays won't use this one until you set it active.", _noticeStyle);

            if (!isActive)
            {
                EditorGUILayout.Space(6);
                if (GUILayout.Button("Set as Active Palette", GUILayout.Height(22)))
                {
                    ColorPaletteSettings.SetActive(palette);
                    EditorApplication.RepaintProjectWindow();
                    EditorApplication.RepaintHierarchyWindow();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawColorsTab(ColorPaletteAsset palette)
        {
            // Guards against a stale index after an Undo or switching to a palette with fewer entries.
            if (_editingCustomIndex >= palette.customColors.Count)
                _editingCustomIndex = -1;

            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Default Colors", "Fixed presets - click one to start a new custom color from it.");
            DrawColorGrid(palette.defaultColors, palette, isDefault: true);
            EditorGUILayout.EndVertical();

            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Custom Colors");
            DrawColorGrid(palette.customColors, palette, isDefault: false);
            if (palette.customColors.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Click to edit in place · Drag to reorder · Right-click to delete", _subtitleStyle);
            }
            EditorGUILayout.EndVertical();

            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
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
            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Your Icon Palette");
            DrawIconPaletteGrid(palette);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Alt+Click a GameObject in the Hierarchy to assign one of these icons to it.", _subtitleStyle);
            EditorGUILayout.EndVertical();

            foreach (var category in IconCatalog.Categories)
            {
                EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
                DrawSectionHeader(category.Name);
                DrawIconCatalogGrid(category.IconNames, palette);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawProjectWindowTab()
        {
            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Project Window Preferences");

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
            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Hierarchy Window Preferences");

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
            EditorGUILayout.LabelField("Matches the color overlay's left edge to the Hierarchy panel's own background. Drag while watching the Hierarchy window.", _subtitleStyle);

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
            EditorGuiKit.BeginCard(_sectionStyle, CardBackgroundColor, CardBorder, CardRadius);
            DrawSectionHeader("Folder Stats Appearance");

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

            DrawSectionHeader(isEditingCustom ? "Editing Custom Color" : "New Color");

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
            EditorGUILayout.LabelField("Hex", _fieldLabelStyle, GUILayout.Width(30));
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
                EditorGUILayout.LabelField("No colors available.", _subtitleStyle);
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
                EditorGUILayout.LabelField("No icons in your palette yet - click one from the categories below to add it.", _subtitleStyle);
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

            EditorGUILayout.LabelField("Right-click an icon to remove it", _subtitleStyle);
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
