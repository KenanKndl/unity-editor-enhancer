using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Quick-switch-scene dropdown, opened from SceneQuickSwitchButton. Lists every scene asset
    /// in the project, with the currently open scene pinned at the top, then favorited scenes,
    /// then the rest - each group separated by a divider.
    ///
    /// Uses ShowAsDropDown (a real EditorWindow), not PopupWindowContent - PopupWindow.Show
    /// closes itself unexpectedly mid press-and-drag gestures.
    /// </summary>
    internal class SceneQuickSwitchPopup : EditorWindow
    {
        private const float RowHeight = 22f;
        private const float SearchFieldHeight = 22f;
        private const float Padding = 8f;
        private const float StarSize = 15f;
        private const float DividerHeight = 9f;
        private const float ScrollbarWidth = 4f;
        private const float ScrollbarGap = 4f;
        private const float DragThreshold = 4f;
        private const string SearchControlName = "SceneQuickSwitchSearch";

        private const float SearchBlockHeight = SearchFieldHeight + 6f;
        private const float CurrentRowBlockHeight = RowHeight + DividerHeight; // current row + divider after it
        private const float MaxListHeight = 280f; // cap for [current row + divider + scrollable groups]
        private const float MaxScrollableHeight = MaxListHeight - CurrentRowBlockHeight;

        // Fixed dark palette - this popup always renders dark regardless of the Editor's skin.
        private static readonly Color BackgroundColor = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color FieldBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color TextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color MutedTextColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color HeartColor = new Color32(0xFA, 0x00, 0x50, 0xFF);

        private static Texture2D _heartFilledTex;
        private static Texture2D _heartOutlinedTex;

        private string _currentScenePath;
        private string _currentSceneName;

        private List<string> _favoritePaths;
        private List<string> _normalPaths;

        private string _searchText = "";
        private Vector2 _scroll;
        private bool _draggingScrollbar;
        private float _dragStartMouseY;
        private float _dragStartScrollY;

        private string _draggedPath;
        private bool _draggedIsFavoriteGroup;
        private Vector2 _pressMousePos;
        private bool _isDraggingRow;

        private bool _focusedSearchOnce;

        private GUIStyle _currentLabelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _searchTextStyle;

        /// <param name="activatorScreenRect">Screen-space rect (not GUI-local) - convert with
        /// GUIUtility.GUIToScreenRect at the call site.</param>
        public static void Show(Rect activatorScreenRect, Scene scene)
        {
            var window = CreateInstance<SceneQuickSwitchPopup>();
            window._currentScenePath = scene.path;
            window._currentSceneName = scene.name;
            window.RefreshGroups();

            Vector2 size = window.ComputeInitialSize();
            window.ShowAsDropDown(activatorScreenRect, size);
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void RefreshGroups()
        {
            List<string> others = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p != _currentScenePath && p.StartsWith("Assets/"))
                .ToList();

            _favoritePaths = SceneOrder.Sort(others.Where(SceneFavorites.IsFavorite));
            _normalPaths = SceneOrder.Sort(others.Where(p => !SceneFavorites.IsFavorite(p)));
        }

        private Vector2 ComputeInitialSize()
        {
            float scrollableHeight = ComputeScrollableContentHeight(_favoritePaths.Count, _normalPaths.Count);
            float scrollHeight = Mathf.Min(scrollableHeight, MaxScrollableHeight);
            float totalHeight = SearchBlockHeight + CurrentRowBlockHeight + scrollHeight + Padding * 2f;
            return new Vector2(260f, totalHeight);
        }

        private static float ComputeScrollableContentHeight(int favoriteCount, int normalCount)
        {
            bool showMidDivider = favoriteCount > 0 && normalCount > 0;
            int rows = favoriteCount + normalCount;
            return rows * RowHeight + (showMidDivider ? DividerHeight : 0f);
        }

        private void EnsureStyles()
        {
            if (_labelStyle == null)
                _labelStyle = new GUIStyle(EditorStyles.label) { padding = new RectOffset(0, 0, 0, 0) };
            _labelStyle.normal.textColor = TextColor;

            if (_currentLabelStyle == null)
                _currentLabelStyle = new GUIStyle(EditorStyles.boldLabel) { padding = new RectOffset(0, 0, 0, 0) };
            _currentLabelStyle.normal.textColor = Color.white;

            if (_searchTextStyle == null)
            {
                // Based on label, not textField - avoids the native field's own background/focus chrome.
                _searchTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(2, 2, 0, 0)
                };
            }
            _searchTextStyle.normal.textColor = TextColor;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!_focusedSearchOnce)
            {
                _focusedSearchOnce = true;
                EditorGUI.FocusTextInControl(SearchControlName);
            }

            Rect rect = new Rect(0f, 0f, position.width, position.height);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, BackgroundColor);

            GUILayout.BeginArea(new Rect(Padding, Padding, rect.width - Padding * 2f, rect.height - Padding * 2f));

            DrawSearchField();
            GUILayout.Space(6f);

            Rect currentRowRect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
            DrawSceneRow(currentRowRect, _currentSceneName, isCurrent: true, path: _currentScenePath, isFavoriteGroup: false);
            DrawSeparator();

            DrawScrollableList();

            GUILayout.EndArea();
        }

        // Positioned manually (GUI.BeginGroup + a running y cursor), not EditorGUILayout.
        // BeginScrollView - that grabs GUIUtility.hotControl as soon as the mouse moves while pressed.
        private void DrawScrollableList()
        {
            // Captured before a row's own Event.Use() below marks this pass's event as Used.
            EventType dragEventType = Event.current.type;

            List<string> visibleFavorites = _favoritePaths.Where(Matches).ToList();
            List<string> visibleNormal = _normalPaths.Where(Matches).ToList();
            bool showMidDivider = visibleFavorites.Count > 0 && visibleNormal.Count > 0;

            float contentHeight = ComputeScrollableContentHeight(visibleFavorites.Count, visibleNormal.Count);
            float scrollHeight = Mathf.Min(contentHeight, MaxScrollableHeight);

            Rect outerRect = GUILayoutUtility.GetRect(0, scrollHeight, GUILayout.ExpandWidth(true));
            bool needsScrollbar = contentHeight > outerRect.height + 0.5f;
            float maxScroll = Mathf.Max(0f, contentHeight - outerRect.height);
            _scroll.y = Mathf.Clamp(_scroll.y, 0f, maxScroll);

            HandleScrollWheel(outerRect, maxScroll);

            GUI.BeginGroup(outerRect);

            float y = -_scroll.y;

            Rect favoritesGroupRect = new Rect(0f, y, outerRect.width, visibleFavorites.Count * RowHeight);
            foreach (string path in visibleFavorites)
            {
                Rect rowRect = new Rect(0f, y, outerRect.width, RowHeight);
                DrawSceneRow(rowRect, Path.GetFileNameWithoutExtension(path), isCurrent: false, path: path, isFavoriteGroup: true);
                y += RowHeight;
            }
            HandleGroupDrag(favoritesGroupRect, visibleFavorites, isFavoriteGroup: true, dragEventType);

            if (showMidDivider)
            {
                DrawSeparatorAt(new Rect(0f, y + DividerHeight / 2f - 0.5f, outerRect.width, 1f));
                y += DividerHeight;
            }

            Rect normalGroupRect = new Rect(0f, y, outerRect.width, visibleNormal.Count * RowHeight);
            foreach (string path in visibleNormal)
            {
                Rect rowRect = new Rect(0f, y, outerRect.width, RowHeight);
                DrawSceneRow(rowRect, Path.GetFileNameWithoutExtension(path), isCurrent: false, path: path, isFavoriteGroup: false);
                y += RowHeight;
            }
            HandleGroupDrag(normalGroupRect, visibleNormal, isFavoriteGroup: false, dragEventType);

            GUI.EndGroup();

            if (needsScrollbar)
                DrawCustomScrollbar(outerRect, contentHeight);
        }

        private void HandleScrollWheel(Rect outerRect, float maxScroll)
        {
            if (Event.current.type != EventType.ScrollWheel || !outerRect.Contains(Event.current.mousePosition))
                return;

            _scroll.y = Mathf.Clamp(_scroll.y + Event.current.delta.y * 20f, 0f, maxScroll);
            Event.current.Use();
            Repaint();
        }

        private void DrawCustomScrollbar(Rect viewportRect, float contentHeight)
        {
            float maxScroll = Mathf.Max(0f, contentHeight - viewportRect.height);
            float thumbHeight = Mathf.Max(20f, viewportRect.height * (viewportRect.height / contentHeight));
            float trackRange = viewportRect.height - thumbHeight;
            float scrollRatio = maxScroll > 0f ? Mathf.Clamp01(_scroll.y / maxScroll) : 0f;

            Rect trackRect = new Rect(viewportRect.xMax - ScrollbarWidth, viewportRect.y, ScrollbarWidth, viewportRect.height);
            Rect thumbRect = new Rect(trackRect.x, trackRect.y + scrollRatio * trackRange, ScrollbarWidth, thumbHeight);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(trackRect, new Color(1f, 1f, 1f, 0.04f));
                EditorGUI.DrawRect(thumbRect, new Color(1f, 1f, 1f, 0.22f));
            }

            if (Event.current.type == EventType.MouseDown && thumbRect.Contains(Event.current.mousePosition))
            {
                _draggingScrollbar = true;
                _dragStartMouseY = Event.current.mousePosition.y;
                _dragStartScrollY = _scroll.y;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag && _draggingScrollbar)
            {
                float deltaY = Event.current.mousePosition.y - _dragStartMouseY;
                float scrollDelta = trackRange > 0f ? deltaY / trackRange * maxScroll : 0f;
                _scroll.y = Mathf.Clamp(_dragStartScrollY + scrollDelta, 0f, maxScroll);
                Event.current.Use();
                Repaint();
            }
            else if (Event.current.type == EventType.MouseUp && _draggingScrollbar)
            {
                _draggingScrollbar = false;
                Event.current.Use();
            }
        }

        private bool Matches(string path)
        {
            if (string.IsNullOrEmpty(_searchText)) return true;
            string name = Path.GetFileNameWithoutExtension(path);
            return name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawSearchField()
        {
            Rect fieldRect = GUILayoutUtility.GetRect(0, SearchFieldHeight, GUILayout.ExpandWidth(true));

            // Flat fill instead of Unity's native textField chrome.
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(fieldRect, FieldBackgroundColor);

            const float iconSize = 13f;
            Rect iconRect = new Rect(fieldRect.x + 6f, fieldRect.y + (fieldRect.height - iconSize) / 2f, iconSize, iconSize);
            Texture searchIcon = EditorGUIUtility.IconContent("d_Search Icon").image;
            if (searchIcon != null)
            {
                var prev = GUI.color;
                GUI.color = MutedTextColor;
                GUI.DrawTexture(iconRect, searchIcon, ScaleMode.ScaleToFit);
                GUI.color = prev;
            }

            bool hasText = !string.IsNullOrEmpty(_searchText);
            float clearWidth = hasText ? 18f : 4f;
            float textX = iconRect.xMax + 4f;
            Rect textRect = new Rect(textX, fieldRect.y, Mathf.Max(0f, fieldRect.xMax - clearWidth - textX), fieldRect.height);

            GUI.SetNextControlName(SearchControlName);
            _searchText = EditorGUI.TextField(textRect, _searchText, _searchTextStyle);

            if (hasText)
            {
                Rect clearRect = new Rect(fieldRect.xMax - 18f, fieldRect.y, 16f, fieldRect.height);
                bool hoveringClear = clearRect.Contains(Event.current.mousePosition);

                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, hoveringClear ? 0.8f : 0.4f);
                GUI.Label(clearRect, "✕", _labelStyle);
                GUI.color = prev;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hoveringClear)
                {
                    Event.current.Use();
                    _searchText = "";
                    GUI.FocusControl(null);
                    EditorGUI.FocusTextInControl(SearchControlName);
                }
            }
        }

        private void DrawSceneRow(Rect rowRect, string name, bool isCurrent, string path, bool isFavoriteGroup)
        {
            bool isBeingDragged = _isDraggingRow && _draggedPath == path;
            bool hoveringRow = !isCurrent && rowRect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint && (hoveringRow || isBeingDragged))
                DrawHighlight(rowRect);

            Texture icon = EditorGUIUtility.IconContent("SceneAsset Icon").image;
            Rect iconRect = new Rect(rowRect.x + 3f, rowRect.y + (RowHeight - 16f) / 2f, 16f, 16f);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon);

            Rect starRect = new Rect(rowRect.xMax - StarSize - 8f, rowRect.y + (RowHeight - StarSize) / 2f, StarSize, StarSize);

            float labelX = iconRect.xMax + 5f;
            float labelWidth = Mathf.Max(0f, starRect.x - 4f - labelX);
            Rect labelRect = new Rect(labelX, rowRect.y, labelWidth, RowHeight);
            GUI.Label(labelRect, name, isCurrent ? _currentLabelStyle : _labelStyle);

            DrawFavoriteStar(starRect, path);

            if (!isCurrent)
                HandleRowPressAndClick(rowRect, starRect, path, isFavoriteGroup);
        }

        // Tracked via the dragged path rather than GUIUtility.hotControl - reordering mid-drag
        // changes row draw order (and therefore control ids), which would break a hotControl match.
        private void HandleRowPressAndClick(Rect rowRect, Rect starRect, string path, bool isFavoriteGroup)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 &&
                rowRect.Contains(e.mousePosition) && !starRect.Contains(e.mousePosition))
            {
                _draggedPath = path;
                _draggedIsFavoriteGroup = isFavoriteGroup;
                _pressMousePos = e.mousePosition;
                _isDraggingRow = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _draggedPath == path)
            {
                if (!_isDraggingRow && (e.mousePosition - _pressMousePos).magnitude > DragThreshold)
                    _isDraggingRow = true;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && _draggedPath == path)
            {
                bool wasDragging = _isDraggingRow;
                _draggedPath = null;
                _isDraggingRow = false;
                e.Use();

                if (!wasDragging)
                {
                    Close();
                    SwitchTo(path);
                }
            }
        }

        // Insertion point follows the cursor's half of the hovered row; past the last row drops at the end.
        private void HandleGroupDrag(Rect groupRect, List<string> groupPaths, bool isFavoriteGroup, EventType dragEventType)
        {
            if (!_isDraggingRow || _draggedPath == null || _draggedIsFavoriteGroup != isFavoriteGroup) return;
            if (dragEventType != EventType.MouseDrag) return;
            if (!groupPaths.Contains(_draggedPath)) return;

            float localY = Event.current.mousePosition.y - groupRect.y;
            float groupHeight = groupPaths.Count * RowHeight;

            if (localY >= groupHeight)
            {
                string last = groupPaths[groupPaths.Count - 1];
                if (last != _draggedPath)
                {
                    SceneOrder.MoveAfter(_draggedPath, last);
                    RefreshGroups();
                    Repaint();
                }
                return;
            }

            int hoveredRow = Mathf.Clamp(Mathf.FloorToInt(localY / RowHeight), 0, groupPaths.Count - 1);
            string hoveredPath = groupPaths[hoveredRow];
            if (hoveredPath == _draggedPath) return;

            bool bottomHalf = localY - hoveredRow * RowHeight >= RowHeight / 2f;
            if (bottomHalf)
                SceneOrder.MoveAfter(_draggedPath, hoveredPath);
            else
                SceneOrder.MoveBefore(_draggedPath, hoveredPath);

            RefreshGroups();
            Repaint();
        }

        private void DrawFavoriteStar(Rect starRect, string path)
        {
            bool isFavorite = SceneFavorites.IsFavorite(path);
            bool hoveringStar = starRect.Contains(Event.current.mousePosition);

            if (_heartFilledTex == null) _heartFilledTex = Resources.Load<Texture2D>("heart-filled");
            if (_heartOutlinedTex == null) _heartOutlinedTex = Resources.Load<Texture2D>("heart-outlined");

            Texture2D heartIcon = isFavorite ? _heartFilledTex : _heartOutlinedTex;
            if (heartIcon == null) return;

            if (Event.current.type == EventType.Repaint && hoveringStar)
            {
                Rect expanded = new Rect(starRect.x - 3f, starRect.y - 3f, starRect.width + 6f, starRect.height + 6f);
                DrawHighlight(expanded);
            }

            var prevColor = GUI.color;
            Color tint = HeartColor;
            if (!isFavorite) tint.a = hoveringStar ? 0.75f : 0.4f;
            GUI.color = tint;
            GUI.DrawTexture(starRect, heartIcon, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hoveringStar)
            {
                Event.current.Use();
                SceneFavorites.Toggle(path);
                RefreshGroups();
                Repaint();
            }
        }

        private static void DrawHighlight(Rect rect)
        {
            EditorGUI.DrawRect(rect, HoverColor);
        }

        private static void DrawSeparator()
        {
            GUILayout.Space(DividerHeight / 2f - 1f);
            Rect r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, SeparatorColor);
        }

        private static void DrawSeparatorAt(Rect r)
        {
            EditorGUI.DrawRect(r, SeparatorColor);
        }

        private static void SwitchTo(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
