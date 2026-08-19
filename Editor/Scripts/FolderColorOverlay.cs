using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    [InitializeOnLoad]
    public static class FolderColorOverlay
    {
        private static readonly Dictionary<string, string> _folderStatsCache = new();
        private static readonly Dictionary<string, bool> _emptyFolderCache = new();

        private static readonly Dictionary<string, (bool hasCustom, bool hasColor, Color color, bool showStats,
            Color textColor, bool hasTextColor, FontStyle textStyle, int fontSize)> _explicitCache = new();
        private static readonly Dictionary<string, (bool hasColor, Color color, int depth)> _inheritedColorCache = new();
        private static readonly Dictionary<string, int> _instanceIdCache = new();

        private static bool _statsNeedRefresh = true;

        private static GUIStyle _folderLabelStyle;
        private static int _folderLabelDefaultFontSize;
        private static GUIStyle _statsLabelStyle;
        private static GUIStyle _gridLabelStyle;
        private static int _gridLabelDefaultFontSize;

        private static MethodInfo _actualImageDrawPositionMethod;
        private static bool _actualImageDrawPositionResolved;
        private static readonly GUIContent _reusableContent = new GUIContent();

        public static void RequestStatsRefresh() => _statsNeedRefresh = true;

        public static void ClearColorCache()
        {
            _explicitCache.Clear();
            _inheritedColorCache.Clear();
            _paletteLookupDone = false;
            _cachedPalette = null;
        }
        
        private static readonly string FolderIconBaseName = "FolderIconBase";
        private static readonly string EmptyFolderIconBaseName = "EmptyFolderIconBase";

        public static Color DefaultEditorColorMainList => EditorGUIUtility.isProSkin ? new Color32(60, 60, 60, 255) : new Color32(200, 200, 200, 255);
        public static Color DefaultEditorColorNoAlphaMainList => new Color(DefaultEditorColorMainList.r, DefaultEditorColorMainList.g, DefaultEditorColorMainList.b, 0f);
        public static Color DefaultEditorSelectedColorMainList => EditorGUIUtility.isProSkin ? new Color32(45, 93, 135, 255) : new Color32(194, 214, 236, 255);
        public static Color DefaultEditorColorGridView => EditorGUIUtility.isProSkin ? new Color32(51, 51, 51, 255) : new Color32(190, 190, 190, 255);
        public static Color DefaultEditorSelectedColorGridView => EditorGUIUtility.isProSkin ? new Color32(45, 93, 135, 255) : new Color32(58, 114, 176, 255);
        public static Color DefaultTextColor => EditorGUIUtility.isProSkin ? new Color32(255, 255, 255, 255) : new Color32(34, 34, 34, 255);

        // Unity's real default (unselected) row label color, same fix already applied in
        // HierarchyColorOverlay - DefaultTextColor above is noticeably brighter than a native,
        // uncolored row's text and was only ever validated against already-colored rows.
        // EditorStyles.label already tracks the active skin on its own, so no ternary is needed.
        private static Color DefaultRowTextColor => EditorStyles.label.normal.textColor;

        static FolderColorOverlay()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.projectChanged += () => 
            { 
                _statsNeedRefresh = true; 
                ClearColorCache();
            };
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            if (string.IsNullOrEmpty(path)) return;
            
            bool isTreeView = selectionRect.height <= 20f && selectionRect.x > 16f;
            bool hasCustomData = TryGetResolvedFolderData(path, isTreeView, out Color folderColor, out bool showStats, out bool hasColor, out int depth);
            
            if (!hasCustomData) return;

            if (selectionRect.height <= 20)
            {
                if (hasColor) 
                    DrawListView(path, guid, selectionRect, folderColor, isTreeView); 
                
                if (showStats) 
                    DrawFolderStats(path, selectionRect); 
            }
            else
            {
                if (hasColor)
                    DrawGridView(path, guid, selectionRect, folderColor);
            }
        }

        private static void DrawListView(string path, string guid, Rect rect, Color color, bool isTreeView)
        {
            bool isSelected = Selection.assetGUIDs.Contains(guid);
            Color bgColor = isSelected ? DefaultEditorSelectedColorMainList : DefaultEditorColorMainList;
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bgColor);

            float bgX = isTreeView ? 14f : rect.x;
            float bgWidth = rect.xMax - bgX;
            float buffer = bgX;

            CachedTextureData cached = GradientTextureCache.GetOrCreate(color, DefaultEditorColorNoAlphaMainList);

            if (cached.ForwardTexture != null)
                GUI.DrawTexture(new Rect(bgX, rect.y, bgWidth, rect.height), cached.ForwardTexture);

            if (cached.BackwardTexture != null && buffer > 0)
                GUI.DrawTexture(new Rect(bgX - buffer, rect.y, buffer, rect.height), cached.BackwardTexture);

            if (isTreeView)
            {
                bool hasSubFolders = AssetDatabase.GetSubFolders(path).Any();
                if (hasSubFolders && Event.current.type == EventType.Repaint)
                {
                    bool isExpanded = IsFolderExpanded(guid, path);
                    Rect foldoutRect = new Rect(rect.x - 14f, rect.y, 14f, rect.height);
                    EditorStyles.foldout.Draw(foldoutRect, false, false, isExpanded, false);
                }
            }

            bool isEmpty = IsFolderEmptyCached(path);

            // AssetDatabase.GetCachedIcon always returns the generic (non-empty) folder icon for
            // a folder - the empty/full distinction is something the native Project Browser
            // decides at paint time, never baked into the asset's own cached icon. Treating that
            // generic icon as "nothing to preserve" lets an empty folder pick up the native empty
            // icon below instead. This intentionally stays on Unity's own built-in icons rather
            // than the package's Resources textures (used by DrawGridView) - those are sized for
            // tinted grid icons and look oversized when drawn at the list view's fixed 16px size.
            Texture iconTex = AssetDatabase.GetCachedIcon(path);
            Texture2D assetIcon = iconTex as Texture2D;
            if (assetIcon == null || assetIcon == EditorGUIUtility.FindTexture("Folder Icon"))
            {
                assetIcon = EditorGUIUtility.FindTexture(isEmpty ? "FolderEmpty Icon" : "Folder Icon") as Texture2D;
            }

            float iconX = isTreeView ? rect.x : rect.x + 2f;
            float labelX = iconX + 16f;

            if (assetIcon != null)
            {
                var prevColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(iconX, rect.y, 16, 16), assetIcon);
                GUI.color = prevColor;
            }

            string folderName = System.IO.Path.GetFileName(path);

            if (_folderLabelStyle == null)
            {
                _folderLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(2, 0, 0, 0)
                };
                _folderLabelDefaultFontSize = _folderLabelStyle.fontSize;
            }
            // Explicit-only text style, no inheritance: read the cache entry populated for this
            // exact path by TryGetResolvedFolderData above, never a parent's. Reset every field
            // on every row rather than only when customized - _folderLabelStyle is a single cached
            // GUIStyle reused across all rows, so a previous row's custom style would otherwise
            // leak onto the next undecorated folder drawn after it.
            _explicitCache.TryGetValue(path, out var textStyleData);
            _folderLabelStyle.normal.textColor = textStyleData.hasTextColor ? textStyleData.textColor : DefaultRowTextColor;
            _folderLabelStyle.fontStyle = textStyleData.textStyle;
            // 0 is the "not customized" storage sentinel, but a GUIStyle's own fontSize=0 means
            // "use the font asset's native point size" - not "keep whatever this style already
            // had" - which is larger than EditorStyles.label's actual default. Fall back to the
            // size captured at construction instead of writing the raw sentinel through.
            _folderLabelStyle.fontSize = textStyleData.fontSize > 0 ? textStyleData.fontSize : _folderLabelDefaultFontSize;

            // rect.xMax, not rect.width: rect.width is the row's own span, not the distance from
            // labelX (an absolute coordinate) to the row's right edge. Using rect.width here goes
            // negative as soon as the panel narrows past labelX, well before it's actually too
            // narrow to show anything - LabelField silently draws nothing for a negative-width
            // rect, leaving the opaque background looking like it swallowed the text.
            float labelWidth = Mathf.Max(0f, rect.xMax - labelX);
            EditorGUI.LabelField(new Rect(labelX, rect.y, labelWidth, rect.height), folderName, _folderLabelStyle);
        }

        private static bool IsFolderExpanded(string guid, string path)
        {
            try
            {
                if (!_instanceIdCache.TryGetValue(guid, out int instanceId))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (obj != null)
                    {
                        instanceId = obj.GetInstanceID();
                        _instanceIdCache[guid] = instanceId;
                    }
                }

                var expandedIds = UnityEditorInternal.InternalEditorUtility.expandedProjectWindowItemIds;
                if (expandedIds != null)
                {
                    foreach (int id in expandedIds)
                    {
                        if (id == instanceId) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void DrawFolderStats(string path, Rect rect)
        {
            string statsText = GetOrComputeStats(path);
            if (string.IsNullOrEmpty(statsText)) return;
            
            Color bgColor = Color.gray;
            Color textColor = Color.white;
            
            ColorPaletteAsset palette = FindPalette();
            if (palette != null)
            {
                bgColor = palette.statsBackgroundColor;
                textColor = palette.statsTextColor;
            }

            if (_statsLabelStyle == null)
            {
                _statsLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }
            _statsLabelStyle.normal.textColor = textColor;

            _reusableContent.text = statsText;
            Vector2 textSize = _statsLabelStyle.CalcSize(_reusableContent);

            string folderName = System.IO.Path.GetFileName(path);
            _reusableContent.text = folderName;
            float nameWidth = EditorStyles.label.CalcSize(_reusableContent).x;
            float labelX = rect.x + 20f;
            float availableSpace = rect.width - labelX - nameWidth - 15f;

            if (availableSpace > textSize.x)
            {
                Rect statsRect = new Rect(rect.xMax - textSize.x - 8f, rect.y + 2f, textSize.x + 4f, rect.height - 4f);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(statsRect, bgColor);
                }

                GUI.Label(statsRect, statsText, _statsLabelStyle);
            }
        }

        private static ColorPaletteAsset _cachedPalette;
        private static bool _paletteLookupDone;

        private static ColorPaletteAsset FindPalette()
        {
            if (_paletteLookupDone && _cachedPalette != null) return _cachedPalette;

            string[] guids = AssetDatabase.FindAssets("t:ColorPaletteAsset");
            _paletteLookupDone = true;
            if (guids.Length == 0) return _cachedPalette = null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return _cachedPalette = AssetDatabase.LoadAssetAtPath<ColorPaletteAsset>(path);
        }

        private static void EnsureStatsFresh()
        {
            if (!_statsNeedRefresh) return;
            _folderStatsCache.Clear();
            _emptyFolderCache.Clear();
            _statsNeedRefresh = false;
        }

        private static bool IsFolderEmptyCached(string path)
        {
            EnsureStatsFresh();

            if (!_emptyFolderCache.TryGetValue(path, out bool isEmpty))
            {
                isEmpty = !AssetDatabase.GetSubFolders(path).Any() &&
                          !AssetDatabase.FindAssets(string.Empty, new[] { path }).Any(g => AssetDatabase.GUIDToAssetPath(g) != path);
                _emptyFolderCache[path] = isEmpty;
            }
            return isEmpty;
        }

        private static string GetOrComputeStats(string path)
        {
            EnsureStatsFresh();

            if (!_folderStatsCache.TryGetValue(path, out string stats))
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                long totalSize = 0;
                int totalItems = 0;
                
                if (System.IO.Directory.Exists(fullPath))
                {
                    GetDirectoryInfoSafe(fullPath, ref totalSize, ref totalItems);
                }

                string itemStr = totalItems <= 1 ? "item" : "items";
                stats = $"{FormatSize(totalSize)} ({totalItems} {itemStr})";
                _folderStatsCache[path] = stats;
            }
            return stats;
        }

        private static void GetDirectoryInfoSafe(string directory, ref long totalSize, ref int totalItems)
        {
            try
            {
                var files = System.IO.Directory.GetFiles(directory, "*", System.IO.SearchOption.TopDirectoryOnly);
                foreach (var f in files)
                {
                    if (f.EndsWith(".meta")) continue; 
                    
                    totalItems++;
                    try { totalSize += new System.IO.FileInfo(f).Length; } catch { }
                }
                var dirs = System.IO.Directory.GetDirectories(directory);
                foreach (var d in dirs)
                {
                    GetDirectoryInfoSafe(d, ref totalSize, ref totalItems);
                }
            }
            catch { }
        }

        private static string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = 1024 * KB;
            const long GB = 1024 * MB;
            if (bytes >= GB) return $"{bytes / (float)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (float)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (float)KB:0.##} KB";
            return $"{bytes} B";
        }

        // Unity's own grid icon rect isn't a simple fraction of the cell - it reserves a fixed
        // strip at the bottom for the label and insets the icon within what's left, so a
        // hand-guessed "80% of the smaller cell dimension" heuristic drifts from the native size
        // and leaves the wrong amount of room above the label. UnityEditor.ObjectListArea's
        // internal LocalGroup.ActualImageDrawPosition is the exact method Unity itself calls for
        // this, so reflecting into it gives a pixel-perfect match instead of guessing. Falls back
        // to the old heuristic if the internal API ever changes shape.
        //
        // referenceIcon should be Unity's own native icon (Folder Icon / FolderEmpty Icon), not
        // the package's custom Resources texture - the native texture's canvas bakes in real
        // padding around the glyph (Unity reserves visual breathing room in every built-in
        // icon), which is what this method needs to reproduce the native size/position.
        private static Rect GetNativeIconRect(Rect cellRect, Texture2D referenceIcon)
        {
            MethodInfo method = GetActualImageDrawPositionMethod();
            if (method != null)
            {
                try
                {
                    return (Rect)method.Invoke(null, new object[] { cellRect, (float)referenceIcon.width, (float)referenceIcon.height });
                }
                catch { /* fall through to the heuristic below */ }
            }

            float iconSize = Mathf.Min(cellRect.width, cellRect.height) * 0.8f;
            float iconX = cellRect.x + (cellRect.width - iconSize) / 2f;
            float iconY = cellRect.y + (cellRect.height - iconSize) / 2f - 5;
            return new Rect(iconX, iconY, iconSize, iconSize);
        }

        // Measured directly off Unity's built-in Folder/FolderEmpty Icon textures (256x256
        // canvas, glyph bounding box (32,48)-(223,207)): 12.5% empty margin on each side
        // horizontally, 18.75% vertically. The package's own FolderIconBase/EmptyFolderIconBase
        // textures are a tight, padding-free crop of that same glyph (made so GUI.color tints
        // cleanly), so drawing them straight into the native icon's own bounding rect - which
        // already has that padding baked in - makes the tinted glyph read as noticeably bigger
        // than an uncolored neighbor. Shrinking the rect by the same fractions before drawing the
        // custom texture reproduces the native glyph's actual on-screen size.
        private const float CustomIconHorizontalPadding = 0.125f;
        private const float CustomIconVerticalPadding = 0.1875f;

        private static Rect InsetForCustomGlyphPadding(Rect nativeCanvasRect)
        {
            float insetX = nativeCanvasRect.width * CustomIconHorizontalPadding;
            float insetY = nativeCanvasRect.height * CustomIconVerticalPadding;
            return new Rect(nativeCanvasRect.x + insetX, nativeCanvasRect.y + insetY,
                nativeCanvasRect.width - insetX * 2f, nativeCanvasRect.height - insetY * 2f);
        }

        private static MethodInfo GetActualImageDrawPositionMethod()
        {
            if (_actualImageDrawPositionResolved) return _actualImageDrawPositionMethod;
            _actualImageDrawPositionResolved = true;
            try
            {
                System.Type listAreaType = typeof(Editor).Assembly.GetType("UnityEditor.ObjectListArea");
                System.Type localGroupType = listAreaType?.GetNestedType("LocalGroup", BindingFlags.NonPublic);
                _actualImageDrawPositionMethod = localGroupType?.GetMethod("ActualImageDrawPosition",
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            catch
            {
                _actualImageDrawPositionMethod = null;
            }
            return _actualImageDrawPositionMethod;
        }

        // Matches "ProjectBrowserGridLabel".fixedHeight (confirmed live via reflection - not
        // ObjectListArea.kListLineHeight, which is a *list* mode row-height constant that only
        // coincidentally looks similar and isn't what grid view actually reserves). GUIStyle's
        // fixedHeight overrides whatever rect height it's given, so drawing the label at any
        // other height than this leaves it slightly mispositioned within its own rect.
        private const float GridLabelHeight = 14f;

        private static void DrawGridView(string path, string guid, Rect rect, Color color)
        {
            bool isSelected = Selection.assetGUIDs.Contains(guid);

            Color gridColor = DefaultEditorColorGridView;
            Color bgTextColor = isSelected ? DefaultEditorSelectedColorGridView : DefaultEditorColorGridView;

            string folderName = System.IO.Path.GetFileName(path);

            // Reuse Unity's own grid label style so the text matches its native format instead of
            // looking bold or oversized.
            GUIStyle originalGridStyle = GUI.skin.FindStyle("ProjectBrowserGridLabel") ?? EditorStyles.miniLabel;

            _reusableContent.text = folderName;
            float nameWidth = originalGridStyle.CalcSize(_reusableContent).x + 6f;
            nameWidth = Mathf.Min(nameWidth, rect.width);
            Rect textRect = new Rect(rect.x + (rect.width - nameWidth) / 2f, rect.yMax - GridLabelHeight, nameWidth, GridLabelHeight);

            // Clear the backgrounds
            EditorGUI.DrawRect(rect, gridColor);
            EditorGUI.DrawRect(textRect, bgTextColor);

            bool isEmpty = IsFolderEmptyCached(path);

            Texture2D nativeIcon = EditorGUIUtility.FindTexture(isEmpty ? "FolderEmpty Icon" : "Folder Icon") as Texture2D;
            Texture2D customIcon = Resources.Load<Texture2D>(isEmpty ? EmptyFolderIconBaseName : FolderIconBaseName);
            Texture2D folderIcon = customIcon != null ? customIcon : nativeIcon;

            if (folderIcon != null)
            {
                // Size/position against the native icon whenever it's available, even when
                // actually drawing the custom (tintable) texture - see GetNativeIconRect and
                // InsetForCustomGlyphPadding for why the two textures can't just share a rect.
                //
                // The icon only ever gets the cell area ABOVE the reserved label strip, never
                // the full cell - passing the full rect here made ActualImageDrawPosition center
                // the icon across the whole cell height, running it down into where the label
                // strip belongs.
                Rect iconAreaRect = new Rect(rect.x, rect.y, rect.width, rect.height - GridLabelHeight);
                Texture2D sizingReference = nativeIcon != null ? nativeIcon : folderIcon;
                Rect nativeCanvasRect = GetNativeIconRect(iconAreaRect, sizingReference);
                Rect iconRect = folderIcon == customIcon ? InsetForCustomGlyphPadding(nativeCanvasRect) : nativeCanvasRect;

                Color prevColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(iconRect, folderIcon, ScaleMode.ScaleToFit, true);
                GUI.color = prevColor;
            }

            // originalGridStyle.normal.textColor is the real native grid label color (confirmed
            // live via reflection: ~0.824 gray, not pure white) - DefaultTextColor is noticeably
            // brighter and was only ever validated for the list view band, same issue already
            // fixed there via DefaultRowTextColor.
            Color labelColor = isSelected ? Color.white : originalGridStyle.normal.textColor;

            // Clone the original style and only adjust its color and alignment
            if (_gridLabelStyle == null)
            {
                _gridLabelStyle = new GUIStyle(originalGridStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                _gridLabelDefaultFontSize = _gridLabelStyle.fontSize;
            }

            // Same explicit-only, always-reset text style as DrawListView - a custom text color
            // wins over both the default and the selected-row white, since the user picked it
            // deliberately for this folder. Same fontSize-sentinel fallback too: see the comment
            // in DrawListView for why 0 can't be written straight through.
            _explicitCache.TryGetValue(path, out var textStyleData);
            _gridLabelStyle.normal.textColor = textStyleData.hasTextColor ? textStyleData.textColor : labelColor;
            _gridLabelStyle.fontStyle = textStyleData.textStyle;
            _gridLabelStyle.fontSize = textStyleData.fontSize > 0 ? textStyleData.fontSize : _gridLabelDefaultFontSize;

            GUI.Label(new Rect(rect.x, rect.yMax - GridLabelHeight, rect.width, GridLabelHeight), folderName, _gridLabelStyle);
        }
        
        private static bool TryGetResolvedFolderData(string path, bool isTreeView, out Color folderColor, out bool showStats, out bool hasColor, out int depth)
        {
            folderColor = Color.white;
            showStats = false;
            hasColor = false;
            depth = 0;

            if (string.IsNullOrEmpty(path)) return false;
            
            if (!_explicitCache.TryGetValue(path, out var explicitData))
            {
                bool hasCustom = FolderMetaData.TryGetFolderData(path, out Color c, out bool s, out bool h,
                    out Color tc, out bool htc, out FontStyle ts, out int fs);
                explicitData = (hasCustom, h, c, s, tc, htc, ts, fs);
                _explicitCache[path] = explicitData;
            }

            hasColor = explicitData.hasColor;
            folderColor = explicitData.color;
            showStats = explicitData.showStats;
            
            if (!FolderTreeSettings.SingleColumnColoring && !hasColor && isTreeView)
            {
                if (_inheritedColorCache.TryGetValue(path, out var inheritedData))
                {
                    folderColor = inheritedData.color;
                    hasColor = inheritedData.hasColor;
                    depth = inheritedData.depth;
                    return hasColor || showStats;
                }

                string parentPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                int currentDepth = 1;

                while (!string.IsNullOrEmpty(parentPath) && parentPath.StartsWith("Assets"))
                {
                    if (!_explicitCache.TryGetValue(parentPath, out var pData))
                    {
                        bool pCustom = FolderMetaData.TryGetFolderData(parentPath, out Color pc, out bool ps, out bool ph,
                            out Color ptc, out bool phtc, out FontStyle pts, out int pfs);
                        pData = (pCustom, ph, pc, ps, ptc, phtc, pts, pfs);
                        _explicitCache[parentPath] = pData;
                    }

                    if (pData.hasColor)
                    {
                        folderColor = pData.color; 
                        hasColor = true;
                        depth = currentDepth;
                        break;
                    }
                    parentPath = System.IO.Path.GetDirectoryName(parentPath)?.Replace('\\', '/');
                    currentDepth++;
                }

                _inheritedColorCache[path] = (hasColor, folderColor, depth);
            }

            return explicitData.hasCustom || hasColor;
        }
    }
}