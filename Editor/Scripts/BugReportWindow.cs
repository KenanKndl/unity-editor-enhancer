using System;
using UnityEditor;
using UnityEngine;

namespace LenzDev.EditorCustomizer
{
    /// <summary>
    /// Standalone "Bug Report / Feedback" utility window. Submitting opens a pre-filled "New
    /// Issue" page on the package's public GitHub repo - there is no backend - so anything typed
    /// here, including the optional contact email, ends up in a public issue.
    /// </summary>
    internal class BugReportWindow : EditorWindow
    {
        private const string GitHubOwner = "KenanKndl";
        private const string GitHubRepo = "unity-editor-enhancer";
        private const string NewIssueUrl = "https://github.com/" + GitHubOwner + "/" + GitHubRepo + "/issues/new";

        private const float CardRadius = 0f;
        private static readonly Color BackgroundColor = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color TabTrackFill = new Color(0.122f, 0.122f, 0.122f, 1f);
        private static readonly Color TabSelectedFill = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color LightTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color MutedTextColor = new Color(0.92f, 0.92f, 0.92f, 0.75f);
        private static readonly Color TabUnselectedText = new Color(0.92f, 0.92f, 0.92f, 0.55f);

        private const string BugDetailsTemplate =
            "## Steps to reproduce:\n1. \n2. \n3. \n\n## What happened?\n\n\n## What should have happened?\n\n";

        private static readonly string[] TabNames = { "Bug Report", "Feedback" };
        private static readonly string[] SeverityOptions = { "Low", "Normal", "High", "Critical" };
        private static readonly string[] FrequencyOptions =
        {
            "This is the first time", "Happens occasionally", "Happens every time"
        };

        private int _selectedTab;
        private string _bugTitle = string.Empty;
        private int _severityIndex = 1;
        private int _frequencyIndex = 0;
        private string _bugEmail = string.Empty;
        private string _bugDetails = BugDetailsTemplate;

        private string _feedbackTitle = string.Empty;
        private string _feedbackEmail = string.Empty;
        private string _feedbackBody = string.Empty;

        private string _statusMessage;

        private GUIStyle _cardStyle;
        private GUIStyle _tabLabelStyle;
        private GUIStyle _tabLabelSelectedStyle;
        private GUIStyle _fieldLabelStyle;
        private GUIStyle _noteStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _statusStyle;

        [MenuItem("Tools/LenzDev/Editor Enhancer/Report a Bug or Send Feedback...")]
        public static void Open()
        {
            var window = GetWindow<BugReportWindow>(true, "Report an Issue");
            // Starting guess only - OnGUI measures the laid-out content and locks min/maxSize to it.
            window.minSize = new Vector2(420, 500);
            window.maxSize = new Vector2(420, 900);
        }

        private void InitStyles()
        {
            if (_cardStyle != null) return;

            _cardStyle = new GUIStyle { padding = new RectOffset(14, 14, 12, 12), margin = new RectOffset(0, 0, 0, 10) };

            _tabLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = TabUnselectedText }
            };
            _tabLabelSelectedStyle = new GUIStyle(_tabLabelStyle) { normal = { textColor = LightTextColor } };

            _fieldLabelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = LightTextColor } };
            _noteStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = MutedTextColor } };
            _textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _statusStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = LightTextColor } };
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BackgroundColor);

            // Wrapped in one outer group so GetLastRect() below can read its total height.
            EditorGUILayout.BeginVertical();

            EditorGUILayout.Space(4);
            DrawTabBar();
            EditorGUILayout.Space(8);

            EditorGuiKit.BeginCard(_cardStyle, BackgroundColor, CardBorder, CardRadius);
            if (_selectedTab == 0) DrawBugReportTab();
            else DrawFeedbackTab();
            EditorGUILayout.EndVertical();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(_statusMessage, _statusStyle);
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
                FitWindowToContent(GUILayoutUtility.GetLastRect().yMax);
        }

        /// <summary>Locks minSize/maxSize height to the measured content height.</summary>
        private void FitWindowToContent(float measuredHeight)
        {
            float desiredHeight = Mathf.Clamp(measuredHeight, 300f, 900f);
            if (Mathf.Abs(minSize.y - desiredHeight) < 1f) return;

            minSize = new Vector2(minSize.x, desiredHeight);
            maxSize = new Vector2(maxSize.x, desiredHeight);
        }

        private void DrawTabBar()
        {
            const float tabBarHeight = 26f;
            Rect barRect = GUILayoutUtility.GetRect(0, tabBarHeight, GUILayout.ExpandWidth(true));
            EditorGuiKit.DrawRoundedRect(barRect, TabTrackFill, CardRadius);

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
                    _statusMessage = null;
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }
        }

        private void DrawBugReportTab()
        {
            EditorGUILayout.LabelField("Title", _fieldLabelStyle);
            _bugTitle = EditorGUILayout.TextField(_bugTitle);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Issue Severity", _fieldLabelStyle);
            _severityIndex = EditorGUILayout.Popup(_severityIndex, SeverityOptions);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("How often does it happen?", _fieldLabelStyle);
            _frequencyIndex = EditorGUILayout.Popup(_frequencyIndex, FrequencyOptions);
            EditorGUILayout.Space(8);

            DrawContactEmailField(ref _bugEmail);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Details", _fieldLabelStyle);
            _bugDetails = EditorGUILayout.TextArea(_bugDetails, _textAreaStyle, GUILayout.Height(150));
            EditorGUILayout.Space(12);

            if (GUILayout.Button("Submit", GUILayout.Height(24)))
                SubmitBugReport();
        }

        private void DrawFeedbackTab()
        {
            EditorGUILayout.LabelField("Title", _fieldLabelStyle);
            _feedbackTitle = EditorGUILayout.TextField(_feedbackTitle);
            EditorGUILayout.Space(8);

            DrawContactEmailField(ref _feedbackEmail);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Feedback", _fieldLabelStyle);
            _feedbackBody = EditorGUILayout.TextArea(_feedbackBody, _textAreaStyle, GUILayout.Height(180));
            EditorGUILayout.Space(12);

            if (GUILayout.Button("Submit", GUILayout.Height(24)))
                SubmitFeedback();
        }

        private void DrawContactEmailField(ref string email)
        {
            EditorGUILayout.LabelField("Contact Email (optional)", _fieldLabelStyle);
            email = EditorGUILayout.TextField(email);
            EditorGUILayout.LabelField(
                "Submitting opens a new issue on the public GitHub repo - anything typed here, " +
                "including this email, will be visible on that page. Leave it blank to stay anonymous.",
                _noteStyle);
        }

        private void SubmitBugReport()
        {
            if (string.IsNullOrWhiteSpace(_bugTitle))
            {
                _statusMessage = "Please enter a title before submitting.";
                return;
            }

            string body =
                $"**Severity:** {SeverityOptions[_severityIndex]}\n" +
                $"**Frequency:** {FrequencyOptions[_frequencyIndex]}\n" +
                $"**Contact Email:** {(string.IsNullOrWhiteSpace(_bugEmail) ? "Not provided" : _bugEmail)}\n\n" +
                $"{_bugDetails}\n\n---\n{BuildFooter()}";

            OpenIssue(_bugTitle, body, "bug");
        }

        private void SubmitFeedback()
        {
            if (string.IsNullOrWhiteSpace(_feedbackTitle))
            {
                _statusMessage = "Please enter a title before submitting.";
                return;
            }

            string body =
                $"**Contact Email:** {(string.IsNullOrWhiteSpace(_feedbackEmail) ? "Not provided" : _feedbackEmail)}\n\n" +
                $"{_feedbackBody}\n\n---\n{BuildFooter()}";

            OpenIssue(_feedbackTitle, body, "feedback");
        }

        private static string BuildFooter()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BugReportWindow).Assembly);
            string version = info != null ? info.version : "unknown";
            return $"*Sent via Unity Editor Enhancer's in-Editor reporter (v{version}, Unity {Application.unityVersion}).*";
        }

        // GitHub's "New Issue" form silently drops query params past its URL-length ceiling.
        private const int MaxBodyLength = 4000;

        private void OpenIssue(string title, string body, string label)
        {
            if (body.Length > MaxBodyLength)
                body = body.Substring(0, MaxBodyLength) + "\n\n...(truncated)";

            string url = $"{NewIssueUrl}?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}&labels={label}";
            Application.OpenURL(url);
            _statusMessage = "Opened in your browser - review the pre-filled issue and press GitHub's own Submit new issue button to finish.";
        }
    }
}
