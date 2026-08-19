using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// A small color palette popup opened via Alt+Left Click on a file/folder, for selection
    /// only (NO adding/removing/dragging).
    /// </summary>
    public class ColorPickerPopup : PopupWindowContent
    {
        private const float SwatchSize = 16f;
        private const float Spacing = 6f;
        private const float Padding = 12f;
        private const float DividerSpacing = 6f;
        private const int Columns = 6;
        private const int DefaultFolderFontSize = 12;

        // Fixed dark palette, deliberately not skin-adaptive - matches SceneQuickSwitchPopup so
        // every popup in the package reads as one consistent, minimal dark UI regardless of the
        // Editor's own Light/Dark skin.
        private static readonly Color BackgroundColor = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color HoverHighlightColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color LightTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        private static readonly Color MutedTextColor = new Color(0.9f, 0.9f, 0.9f, 0.6f);

        private GUIStyle _boldLabelStyle;
        private GUIStyle _miniBoldLabelStyle;
        private GUIStyle _helpLabelStyle;

        private GUIStyle BoldLabelStyle()
        {
            if (_boldLabelStyle == null)
                _boldLabelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = LightTextColor } };
            return _boldLabelStyle;
        }

        private GUIStyle MiniBoldLabelStyle()
        {
            if (_miniBoldLabelStyle == null)
                _miniBoldLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = LightTextColor } };
            return _miniBoldLabelStyle;
        }

        private GUIStyle HelpLabelStyle()
        {
            if (_helpLabelStyle == null)
                _helpLabelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = MutedTextColor } };
            return _helpLabelStyle;
        }

        /// <summary>
        /// Extra controls shown only when the popup is opened for a Header-kind
        /// HierarchyOrganizerMarker: font style (a plain toolbar, so it never closes the popup -
        /// same non-closing behavior as the stats toggle below) plus a second, smaller color
        /// grid wired to the header's own text color instead of the background/accent color.
        /// </summary>
        private class TextStyleOptions
        {
            public System.Func<FontStyle> GetStyle;
            public System.Action<FontStyle> SetStyle;
            public System.Func<HierarchyOrganizerMarker.TextAlign> GetAlign;
            public System.Action<HierarchyOrganizerMarker.TextAlign> SetAlign;
            public System.Func<int> GetFontSize;
            public System.Action<int> SetFontSize;
            public System.Action<Color> SetColor;
            public System.Action ClearColor;
        }

        private readonly ColorPaletteAsset _palette;
        private readonly System.Action<Color> _applyColor;
        private readonly System.Action _clearColor;
        private readonly System.Action<bool> _setShowStats;
        private readonly TextStyleOptions _textStyle;
        private readonly System.Action<string> _applyIcon;
        private readonly System.Action _clearIcon;

        private bool _showStats;
        private bool _textStyleExpanded;

        private ColorPickerPopup(ColorPaletteAsset palette, System.Action<Color> applyColor, System.Action clearColor,
            bool showStats, System.Action<bool> setShowStats, TextStyleOptions textStyle = null,
            System.Action<string> applyIcon = null, System.Action clearIcon = null)
        {
            _palette = palette;
            _applyColor = applyColor;
            _clearColor = clearColor;
            _showStats = showStats;
            _setShowStats = setShowStats;
            _textStyle = textStyle;
            _applyIcon = applyIcon;
            _clearIcon = clearIcon;
        }

        /// <summary>
        /// Opens the popup at the given position for a folder/file asset. If no ColorPaletteAsset
        /// exists in the project, logs a warning and doesn't open.
        /// </summary>
        public static void Show(Rect activatorRect, string targetPath)
        {
            ColorPaletteAsset palette = FindPalette();
            if (palette == null)
            {
                WarnNoPalette();
                return;
            }

            FolderMetaData.TryGetFolderData(targetPath, out _, out bool showStats, out _,
                out _, out _, out _, out _);

            var textStyle = new TextStyleOptions
            {
                GetStyle = () =>
                {
                    FolderMetaData.TryGetFolderData(targetPath, out _, out _, out _, out _, out _, out FontStyle s, out _);
                    return s;
                },
                SetStyle = style => FolderMetaData.SetFolderTextStyle(targetPath, style),
                GetFontSize = () =>
                {
                    // 0 means "not customized" in storage (FolderColorOverlay leaves the label's
                    // own GUIStyle.fontSize alone for that case) but the Font Size slider's [8,32]
                    // range can't display 0 - clamp it there and back, which would silently write
                    // a "customized" size the moment the popup opens even without user input. Show
                    // a real in-range default instead so the slider stays a no-op until dragged.
                    FolderMetaData.TryGetFolderData(targetPath, out _, out _, out _, out _, out _, out _, out int size);
                    return size == 0 ? DefaultFolderFontSize : size;
                },
                SetFontSize = size => FolderMetaData.SetFolderFontSize(targetPath, size),
                SetColor = color => FolderMetaData.SetFolderTextColor(targetPath, color),
                ClearColor = () => FolderMetaData.ClearFolderTextColor(targetPath)
            };

            var popup = new ColorPickerPopup(palette,
                color => FolderMetaData.SetFolderColor(targetPath, color),
                () => FolderMetaData.ClearFolderColor(targetPath),
                showStats,
                show => FolderMetaData.SetFolderStats(targetPath, show),
                textStyle);

            PopupWindow.Show(activatorRect, popup);
        }

        /// <summary>
        /// Opens the popup at the given position for a Hierarchy GameObject. If no
        /// ColorPaletteAsset exists in the project, logs a warning and doesn't open.
        /// </summary>
        public static void Show(Rect activatorRect, GameObject targetObject)
        {
            ColorPaletteAsset palette = FindPalette();
            if (palette == null)
            {
                WarnNoPalette();
                return;
            }

            TextStyleOptions textStyle = null;
            HierarchyOrganizerMarker organizer = targetObject.GetComponent<HierarchyOrganizerMarker>();
            if (organizer != null && organizer.kind == HierarchyOrganizerMarker.OrganizerKind.Header)
            {
                textStyle = new TextStyleOptions
                {
                    GetStyle = () => organizer.textStyle,
                    SetStyle = style => HierarchyOrganizerMetaData.SetTextStyle(targetObject, style),
                    GetAlign = () => organizer.textAlign,
                    SetAlign = align => HierarchyOrganizerMetaData.SetTextAlign(targetObject, align),
                    GetFontSize = () => organizer.fontSize,
                    SetFontSize = size => HierarchyOrganizerMetaData.SetFontSize(targetObject, size),
                    SetColor = color => HierarchyOrganizerMetaData.SetTextColor(targetObject, color),
                    ClearColor = () => HierarchyOrganizerMetaData.ClearTextColor(targetObject)
                };
            }

            var popup = new ColorPickerPopup(palette,
                color => HierarchyMetaData.SetColor(targetObject, color),
                () => HierarchyMetaData.ClearColor(targetObject),
                showStats: false,
                setShowStats: null,
                textStyle,
                applyIcon: iconName => HierarchyMetaData.SetIcon(targetObject,
                    EditorGUIUtility.IconContent(iconName)?.image as Texture2D),
                clearIcon: () => HierarchyMetaData.ClearIcon(targetObject));

            PopupWindow.Show(activatorRect, popup);
        }

        private static void WarnNoPalette()
        {
            Debug.LogWarning("No ColorPaletteAsset was found in the project. " +
                              "Create one first via 'Create > Editor Customizer > Color Palette'.");
        }

        private static ColorPaletteAsset FindPalette()
        {
            string[] guids = AssetDatabase.FindAssets("t:ColorPaletteAsset");
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ColorPaletteAsset>(path);
        }

        public override Vector2 GetWindowSize()
        {
            bool hasCustom = _palette.customColors.Count > 0;

            // Top group: leading clear + defaults (settings button lives in its own fixed row now)
            int defaultSlots = _palette.defaultColors.Count + 1;
            int rowsDefault = Mathf.CeilToInt((float)defaultSlots / Columns);

            // Bottom group: custom colors (if any)
            int customSlots = hasCustom ? _palette.customColors.Count : 0;
            int rowsCustom = customSlots > 0 ? Mathf.CeilToInt((float)customSlots / Columns) : 0;

            float gridWidth = Columns * SwatchSize + (Columns - 1) * Spacing;

            float heightDefault = rowsDefault * SwatchSize + (rowsDefault - 1) * Spacing;
            float heightCustom = rowsCustom > 0 ? rowsCustom * SwatchSize + (rowsCustom - 1) * Spacing : 0f;
            float dividerBlock = rowsCustom > 0 ? (DividerSpacing * 2 + 1f) : 0f;

            float width = gridWidth + Padding * 2;
            float height = heightDefault + dividerBlock + heightCustom + Padding * 2;
            height += SwatchSize + Spacing; // fixed top-right settings button row
            if (_setShowStats != null) height += 24f;

            if (_textStyle != null)
            {
                // Row height is singleLineHeight (18) plus each EditorGUILayout control style's own
                // top/bottom margin, which GUILayout adds on top - 20f alone was cutting the grid
                // below it a few pixels short, so this rounds up generously rather than chasing an
                // exact IMGUI margin figure.
                height += DividerSpacing * 2 + 1f;     // divider ahead of the text style section
                height += 16f + 4f;                    // foldout header row + spacing

                if (_textStyleExpanded)
                {
                    int textSlots = 1 + _palette.defaultColors.Count + _palette.customColors.Count; // +1 leading clear
                    int textRows = Mathf.CeilToInt((float)textSlots / Columns);
                    float textGridHeight = textRows * SwatchSize + (textRows - 1) * Spacing;

                    height += (_textStyle.GetAlign != null ? 3 : 2) * 22f; // Style / [Alignment] / Font Size rows
                    height += 8f;                          // spacing before the text color label
                    height += 16f + 4f;                    // "Text Color" label + spacing
                    height += textGridHeight + 10f;        // extra trailing buffer so the grid isn't flush against the edge
                }
            }

            if (_applyIcon != null)
            {
                int iconSlots = 1 + _palette.customIcons.Count; // +1 leading clear
                int iconRows = Mathf.CeilToInt((float)iconSlots / Columns);
                float iconGridHeight = iconRows * SwatchSize + (iconRows - 1) * Spacing;

                height += DividerSpacing * 2 + 1f; // divider ahead of the icon section
                height += 16f + 4f;                // "Icon" label + spacing
                height += iconGridHeight + 6f;
            }

            // The header extension packs three labeled dropdown/slider rows - the narrow grid-only
            // width leaves no room for their labels, so give it more breathing room.
            float minWidth = _textStyle != null ? 210f : 140f;

            return new Vector2(Mathf.Max(width, minWidth), Mathf.Max(height, 40f));
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, BackgroundColor);

            // Inset the content by the padding amount
            GUILayout.BeginArea(new Rect(Padding, Padding, rect.width - Padding * 2, rect.height - Padding * 2));

            DrawSettingsButtonRow();

            bool hasCustom = _palette.customColors.Count > 0;

            // Top group: X button + default colors
            DrawGrid(_palette.defaultColors, includeLeadingClear: true, _applyColor, _clearColor);

            // Bottom group: custom colors (if any, separated by a thin divider)
            if (hasCustom)
            {
                GUILayout.Space(DividerSpacing);
                DrawDivider();
                GUILayout.Space(DividerSpacing);
                DrawGrid(_palette.customColors, includeLeadingClear: false, _applyColor, _clearColor);
            }
            if (_setShowStats != null)
            {
                GUILayout.Space(8);
                EditorGUI.BeginChangeCheck();
                _showStats = EditorGUILayout.ToggleLeft("Show folder stats", _showStats, BoldLabelStyle());
                if (EditorGUI.EndChangeCheck())
                {
                    _setShowStats(_showStats);
                }
            }

            if (_textStyle != null)
                DrawTextStyleSection();

            if (_applyIcon != null)
                DrawIconSection();

            GUILayout.EndArea();

            // PopupWindowContent only sizes itself from GetWindowSize() once, at open time - the
            // Text Style foldout can change content height afterward, so keep the window synced.
            if (Event.current.type == EventType.Repaint)
            {
                Vector2 desired = GetWindowSize();
                if (editorWindow.position.size != desired)
                {
                    editorWindow.minSize = desired;
                    editorWindow.maxSize = desired;
                }
            }
        }

        /// <summary>
        /// Icon override for the GameObject itself (any GameObject, not just Header organizers -
        /// unlike the text style section). Only ever shown from the GameObject overload of Show;
        /// folder popups never set _applyIcon, so this section stays hidden for them.
        /// </summary>
        private void DrawIconSection()
        {
            GUILayout.Space(DividerSpacing);
            DrawDivider();
            GUILayout.Space(DividerSpacing);

            EditorGUILayout.LabelField("Icon", MiniBoldLabelStyle());

            List<string> icons = _palette.customIcons;
            int totalSlots = icons.Count + 1; // +1 leading clear/reset swatch
            int rows = Mathf.CeilToInt((float)totalSlots / Columns);
            int index = 0;

            for (int r = 0; r < rows; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < Columns; c++)
                {
                    if (index >= totalSlots)
                    {
                        GUILayout.Space(SwatchSize + Spacing);
                    }
                    else if (index == 0)
                    {
                        DrawClearIconSwatch();
                        if (c < Columns - 1) GUILayout.Space(Spacing);
                    }
                    else
                    {
                        DrawIconSwatch(icons[index - 1]);
                        if (c < Columns - 1) GUILayout.Space(Spacing);
                    }
                    index++;
                }
                GUILayout.EndHorizontal();
                if (r < rows - 1)
                    GUILayout.Space(Spacing);
            }

            if (icons.Count == 0)
                EditorGUILayout.LabelField("Add icons from the palette's Icons tab.", HelpLabelStyle());
        }

        private void DrawIconSwatch(string iconName)
        {
            Rect rect = ReserveSwatchRect();

            DrawHoverHighlight(rect);

            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);

            HandleClick(rect, () => _applyIcon(iconName));
        }

        private void DrawClearIconSwatch()
        {
            Rect rect = ReserveSwatchRect();

            DrawHoverHighlight(rect);

            var xStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            Color prevContentColor = GUI.contentColor;
            GUI.contentColor = LightTextColor;
            GUI.Label(rect, "✕", xStyle);
            GUI.contentColor = prevContentColor;

            HandleClick(rect, () => _clearIcon?.Invoke());
        }

        private void DrawTextStyleSection()
        {
            GUILayout.Space(DividerSpacing);
            DrawDivider();
            GUILayout.Space(DividerSpacing);

            // Collapsed by default (a fresh popup instance is created every time it's opened, so
            // this always starts collapsed) - clicking the header toggles it without closing the
            // popup, same as every other control here.
            Rect headerRect = GUILayoutUtility.GetRect(0, 16f, GUILayout.ExpandWidth(true));
            string arrow = _textStyleExpanded ? "▾" : "▸"; // ▾ / ▸
            GUI.Label(headerRect, arrow + " Text Style", MiniBoldLabelStyle());
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                _textStyleExpanded = !_textStyleExpanded;
                Event.current.Use();
            }

            if (!_textStyleExpanded)
                return;

            GUILayout.Space(4);

            // Plain EditorGUILayout controls (self-labeled dropdown/slider), same as the stats
            // toggle above: none of these close the popup, so style/alignment/size/color can all
            // be adjusted in one sitting before the popup is dismissed via a color swatch pick.
            //
            // EditorGUIUtility.labelWidth defaults to a fraction of the *host view's* width, not
            // this popup's - in this narrow popup that leaves almost nothing for the actual
            // dropdown/slider value, truncating "Bold Italic" down to a couple of letters. Narrow
            // it just for these rows so the value gets the room it needs without widening the
            // popup itself.
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Style", GUILayout.Width(70f));
            FontStyle currentStyle = _textStyle.GetStyle();
            bool isBold = currentStyle == FontStyle.Bold || currentStyle == FontStyle.BoldAndItalic;
            bool isItalic = currentStyle == FontStyle.Italic || currentStyle == FontStyle.BoldAndItalic;
            var boldButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft) { fontStyle = FontStyle.Bold };
            var italicButtonStyle = new GUIStyle(EditorStyles.miniButtonRight) { fontStyle = FontStyle.Italic };
            bool newBold = GUILayout.Toggle(isBold, "B", boldButtonStyle, GUILayout.Width(24f));
            bool newItalic = GUILayout.Toggle(isItalic, "I", italicButtonStyle, GUILayout.Width(24f));
            EditorGUILayout.EndHorizontal();
            if (newBold != isBold || newItalic != isItalic)
            {
                FontStyle result;
                if (newBold && newItalic) result = FontStyle.BoldAndItalic;
                else if (newBold) result = FontStyle.Bold;
                else if (newItalic) result = FontStyle.Italic;
                else result = FontStyle.Normal;
                _textStyle.SetStyle(result);
            }

            if (_textStyle.GetAlign != null)
            {
                var newAlign = (HierarchyOrganizerMarker.TextAlign)EditorGUILayout.EnumPopup("Alignment", _textStyle.GetAlign());
                if (newAlign != _textStyle.GetAlign())
                    _textStyle.SetAlign(newAlign);
            }

            int newSize = EditorGUILayout.IntSlider("Font Size", _textStyle.GetFontSize(), 8, 32);
            if (newSize != _textStyle.GetFontSize())
                _textStyle.SetFontSize(newSize);

            EditorGUIUtility.labelWidth = prevLabelWidth;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Text Color", MiniBoldLabelStyle());

            // One merged grid (defaults + custom colors, leading clear only - the settings button
            // is a fixed top-right row now, not repeated per grid) wired to the header's text
            // color instead of _applyColor/_clearColor.
            List<Color> merged = new List<Color>(_palette.defaultColors.Count + _palette.customColors.Count);
            merged.AddRange(_palette.defaultColors);
            merged.AddRange(_palette.customColors);
            DrawGrid(merged, includeLeadingClear: true, _textStyle.SetColor, _textStyle.ClearColor);
        }

        private void DrawGrid(List<Color> colors, bool includeLeadingClear,
            System.Action<Color> applyColor, System.Action clearColor)
        {
            int leadingCount = includeLeadingClear ? 1 : 0;
            int totalSlots = colors.Count + leadingCount;

            if (totalSlots == 0)
                return;

            int rows = Mathf.CeilToInt((float)totalSlots / Columns);
            int index = 0;

            for (int r = 0; r < rows; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < Columns; c++)
                {
                    if (index >= totalSlots)
                    {
                        GUILayout.Space(SwatchSize + Spacing);
                    }
                    else if (includeLeadingClear && index == 0)
                    {
                        DrawClearSwatch(clearColor);
                        if (c < Columns - 1)
                            GUILayout.Space(Spacing);
                    }
                    else
                    {
                        int colorIndex = index - leadingCount;
                        DrawColorSwatch(colors[colorIndex], applyColor);
                        if (c < Columns - 1)
                            GUILayout.Space(Spacing);
                    }
                    index++;
                }
                GUILayout.EndHorizontal();
                if (r < rows - 1)
                    GUILayout.Space(Spacing);
            }
        }

        private static void DrawDivider()
        {
            Rect r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUI.DrawRect(r, DividerColor);
        }

        private void DrawColorSwatch(Color color, System.Action<Color> applyColor)
        {
            Rect rect = ReserveSwatchRect();

            DrawHoverHighlight(rect);

            Texture2D tex = RoundedTextureProvider.Get();
            if (tex != null)
            {
                var prevColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(rect, tex);
                GUI.color = prevColor;
            }
            else
            {
                EditorGUI.DrawRect(rect, color);
            }

            HandleClick(rect, () => applyColor(color));
        }

        private void DrawClearSwatch(System.Action clearColor)
        {
            Rect rect = ReserveSwatchRect();

            DrawHoverHighlight(rect);

            // Cross mark in the middle (no background, icon only)
            var xStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            Color prevContentColor = GUI.contentColor;
            GUI.contentColor = LightTextColor;
            GUI.Label(rect, "✕", xStyle);
            GUI.contentColor = prevContentColor;

            HandleClick(rect, () => clearColor?.Invoke());
        }

        /// <summary>
        /// Reserves a full-width row and draws the settings (gear) button pinned to its right
        /// edge - a fixed, always-visible top-right position rather than a swatch buried at the
        /// end of whichever color grid happened to be drawn last, which was easy to miss.
        /// </summary>
        private void DrawSettingsButtonRow()
        {
            Rect rowRect = GUILayoutUtility.GetRect(0, SwatchSize, GUILayout.ExpandWidth(true));
            Rect gearRect = new Rect(rowRect.xMax - SwatchSize, rowRect.y, SwatchSize, SwatchSize);
            DrawSettingsSwatch(gearRect);
            GUILayout.Space(Spacing);
        }

        private void DrawSettingsSwatch(Rect rect)
        {
            DrawHoverHighlight(rect);

            // Gear mark in the middle (no background, icon only)
            var gearStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            Color prevContentColor = GUI.contentColor;
            GUI.contentColor = LightTextColor;
            GUI.Label(rect, "⚙", gearStyle);
            GUI.contentColor = prevContentColor;

            HandleClick(rect, OpenPaletteSettings);
        }

        /// <summary>
        /// Called when the settings (gear) button is clicked: opens the palette's ScriptableObject
        /// Inspector in a separate, focused window.
        /// </summary>
        private void OpenPaletteSettings()
        {
            EditorUtility.OpenPropertyEditor(_palette);
        }

        private static Rect ReserveSwatchRect()
        {
            return GUILayoutUtility.GetRect(SwatchSize, SwatchSize, GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));
        }

        private static void DrawHoverHighlight(Rect rect)
        {
            if (!rect.Contains(Event.current.mousePosition))
                return;

            // Draw a subtle highlight around the swatch, in the same rounded shape.
            // This highlight must be drawn BEFORE the actual content (color/icon) so that the
            // content on top stays crisp and doesn't look mismatched, like a square frame.
            Rect expanded = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);

            Color highlight = HoverHighlightColor;

            Texture2D tex = RoundedTextureProvider.Get();
            if (tex != null)
            {
                var prevColor = GUI.color;
                GUI.color = highlight;
                GUI.DrawTexture(expanded, tex);
                GUI.color = prevColor;
            }
            else
            {
                EditorGUI.DrawRect(expanded, highlight);
            }
        }

        private void HandleClick(Rect rect, System.Action onClick)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                onClick();
                Event.current.Use();
                editorWindow.Close();
            }
        }

    }
}
