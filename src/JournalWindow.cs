using System;
using System.Text;
using UnityEngine;

namespace ErenshorJournal
{
    internal sealed class JournalWindow
    {
        private const int WindowId = 0x45524A4E;
        private const float HeaderHeight = 31f;
        private const int ChronicleVisibleLimit = 200;

        private JournalDocument _document;
        private Action _markDirty;
        private bool _showChronicle;
        private bool _requestClose;
        private Vector2 _tabScroll;
        private Vector2 _chronicleScroll;
        private float _deleteArmedUntil;
        private float _clearChronicleArmedUntil;
        private Rect _currentWindowRect;
        private bool _resizing;
        private Vector2 _resizeDelta;
        private bool _textInputFocused;

        private const string TabNameControl = "ErenshorJournal.TabName";
        private const string NoteTextControl = "ErenshorJournal.NoteText";

        // True while the tab-name field or the note text area actually has keyboard focus, not
        // merely while the window is open. Used to suppress native movement/hotkey input only
        // for as long as the player is actually typing into the journal.
        internal bool IsTextInputFocused
        {
            get { return _textInputFocused; }
        }

        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _selectedTexture;
        private Texture2D _dangerTexture;
        private Texture2D _dangerHoverTexture;
        private Texture2D _textTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _selectedTabStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _chronicleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _dangerButtonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _resizeGripStyle;

        internal bool RequestClose
        {
            get { return _requestClose; }
        }

        internal Rect Draw(Rect rect, JournalDocument document, Action markDirty)
        {
            EnsureStyles();
            _document = document;
            _markDirty = markDirty;
            _requestClose = false;
            _currentWindowRect = rect;
            _resizeDelta = Vector2.zero;

            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                GUI.depth = -60;
                result = GUI.Window(WindowId, rect, DrawWindowContents, GUIContent.none, _windowStyle);
            }
            finally
            {
                GUI.depth = previousDepth;
            }

            if (_resizeDelta != Vector2.zero)
            {
                result.width += _resizeDelta.x;
                result.height += _resizeDelta.y;
            }

            string focused = GUI.GetNameOfFocusedControl();
            _textInputFocused = !_showChronicle &&
                (string.Equals(focused, TabNameControl, StringComparison.Ordinal) ||
                 string.Equals(focused, NoteTextControl, StringComparison.Ordinal));

            return result;
        }

        internal void Dispose()
        {
            DestroyTexture(ref _panelTexture);
            DestroyTexture(ref _buttonTexture);
            DestroyTexture(ref _buttonHoverTexture);
            DestroyTexture(ref _selectedTexture);
            DestroyTexture(ref _dangerTexture);
            DestroyTexture(ref _dangerHoverTexture);
            DestroyTexture(ref _textTexture);
            _windowStyle = null;
            _titleStyle = null;
            _sectionStyle = null;
            _textAreaStyle = null;
            _textFieldStyle = null;
            _tabStyle = null;
            _selectedTabStyle = null;
            _footerStyle = null;
            _chronicleStyle = null;
            _buttonStyle = null;
            _dangerButtonStyle = null;
            _closeButtonStyle = null;
            _resizeGripStyle = null;
            _textInputFocused = false;
        }

        private void DrawWindowContents(int id)
        {
            GUILayout.BeginVertical();
            DrawHeader();
            GUILayout.Space(2f);
            DrawTabs();
            GUILayout.Space(2f);

            if (_showChronicle) DrawChronicle();
            else DrawSelectedTab();

            GUILayout.EndVertical();
            DrawResizeGrip();

            // Dragging is limited to the title bar. Buttons and note contents do
            // not double as drag surfaces.
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, _currentWindowRect.width - 42f), HeaderHeight));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(HeaderHeight));
            GUILayout.Label("ERENSHOR JOURNAL", _titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(28f), GUILayout.Height(22f)))
                _requestClose = true;
            GUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, false, false, GUILayout.Height(36f));
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Chronicle", _showChronicle ? _selectedTabStyle : _tabStyle, GUILayout.MinWidth(90f), GUILayout.Height(27f)))
            {
                _showChronicle = true;
                _deleteArmedUntil = 0f;
            }

            for (int i = 0; i < _document.Tabs.Count; i++)
            {
                JournalTab tab = _document.Tabs[i];
                bool selected = !_showChronicle && i == _document.SelectedTabIndex;
                if (GUILayout.Button(tab.Name, selected ? _selectedTabStyle : _tabStyle, GUILayout.MinWidth(80f), GUILayout.Height(27f)))
                {
                    _showChronicle = false;
                    _document.SelectedTabIndex = i;
                    _deleteArmedUntil = 0f;
                    MarkDirty();
                }
            }

            if (GUILayout.Button("+", _tabStyle, GUILayout.Width(30f), GUILayout.Height(27f)))
            {
                if (JournalCore.AddTab(_document))
                {
                    _showChronicle = false;
                    _deleteArmedUntil = 0f;
                    MarkDirty();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawSelectedTab()
        {
            if (_document.Tabs.Count == 0) return;
            JournalTab tab = _document.Tabs[_document.SelectedTabIndex];

            GUILayout.BeginHorizontal();
            GUILayout.Label("PAGE", _sectionStyle, GUILayout.Width(38f));
            GUI.SetNextControlName(TabNameControl);
            string newName = GUILayout.TextField(tab.Name, JournalCore.MaxTabNameLength, _textFieldStyle, GUILayout.MinWidth(130f), GUILayout.Height(24f));
            if (!string.Equals(newName, tab.Name, StringComparison.Ordinal))
            {
                tab.Name = JournalCore.CleanTabName(newName);
                MarkDirty();
            }

            if (GUILayout.Button("Timestamp", _buttonStyle, GUILayout.Width(78f), GUILayout.Height(24f)))
            {
                string prefix = string.IsNullOrEmpty(tab.Text) || tab.Text.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : Environment.NewLine;
                tab.Text += prefix + "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "] ";
                MarkDirty();
            }

            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(50f), GUILayout.Height(24f)))
                GUIUtility.systemCopyBuffer = tab.Text ?? string.Empty;

            bool armed = Time.unscaledTime < _deleteArmedUntil;
            string deleteLabel = armed ? "Confirm" : "Delete";
            if (GUILayout.Button(deleteLabel, _dangerButtonStyle, GUILayout.Width(64f), GUILayout.Height(24f)))
            {
                if (!armed)
                {
                    _deleteArmedUntil = Time.unscaledTime + 4f;
                }
                else if (JournalCore.DeleteSelectedTab(_document))
                {
                    _deleteArmedUntil = 0f;
                    MarkDirty();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);

            string oldText = tab.Text ?? string.Empty;
            GUI.SetNextControlName(NoteTextControl);
            string newText = GUILayout.TextArea(oldText, _textAreaStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                tab.Text = newText;
                MarkDirty();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Local notes - autosaved", _footerStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label((tab.Text == null ? 0 : tab.Text.Length).ToString() + " chars", _footerStyle, GUILayout.Width(80f));
            GUILayout.EndHorizontal();
        }

        private void DrawChronicle()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("CHRONICLE", _sectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(_document.Chronicle.Count.ToString() + " entries", _footerStyle, GUILayout.Width(75f));

            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(50f), GUILayout.Height(24f)))
                GUIUtility.systemCopyBuffer = BuildChronicleText(_document);

            bool armed = Time.unscaledTime < _clearChronicleArmedUntil;
            string clearLabel = armed ? "Confirm" : "Clear";
            if (GUILayout.Button(clearLabel, _dangerButtonStyle, GUILayout.Width(64f), GUILayout.Height(24f)))
            {
                if (!armed)
                {
                    _clearChronicleArmedUntil = Time.unscaledTime + 4f;
                }
                else
                {
                    _document.Chronicle.Clear();
                    _clearChronicleArmedUntil = 0f;
                    MarkDirty();
                }
            }
            GUILayout.EndHorizontal();

            _chronicleScroll = GUILayout.BeginScrollView(_chronicleScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            int start = Math.Max(0, _document.Chronicle.Count - ChronicleVisibleLimit);
            if (_document.Chronicle.Count == 0)
            {
                GUILayout.Label("No Chronicle entries yet. Compatible mods can append verified events through JournalApi; your normal tabs remain fully player-owned.", _chronicleStyle);
            }
            else
            {
                for (int i = start; i < _document.Chronicle.Count; i++)
                {
                    JournalChronicleEntry entry = _document.Chronicle[i];
                    DateTime local = entry.TimestampUtc.Kind == DateTimeKind.Utc ? entry.TimestampUtc.ToLocalTime() : entry.TimestampUtc;
                    string prefix = local.ToString("yyyy-MM-dd HH:mm");
                    if (!string.IsNullOrWhiteSpace(entry.Category)) prefix += "  [" + entry.Category + "]";
                    if (!string.IsNullOrWhiteSpace(entry.Source)) prefix += "  " + entry.Source;
                    GUILayout.Label(prefix + Environment.NewLine + entry.Text, _chronicleStyle);
                    GUILayout.Space(6f);
                }
            }
            GUILayout.EndScrollView();

            if (start > 0)
                GUILayout.Label("Showing the latest " + ChronicleVisibleLimit.ToString() + " entries; older entries remain saved and are included by Copy.", _footerStyle);
            else
                GUILayout.Label("Append-only integration history; the Journal itself does not infer game events.", _footerStyle);
        }

        private void DrawResizeGrip()
        {
            Rect grip = new Rect(Mathf.Max(0f, _currentWindowRect.width - 22f), Mathf.Max(0f, _currentWindowRect.height - 20f), 18f, 16f);
            GUI.Label(grip, "//", _resizeGripStyle);

            Event current = Event.current;
            if (current == null) return;

            if (!_resizing && current.type == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                _resizing = true;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseDrag && current.button == 0)
            {
                _resizeDelta += current.delta;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseUp && current.button == 0)
            {
                _resizing = false;
                current.Use();
            }
        }

        private static string BuildChronicleText(JournalDocument document)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < document.Chronicle.Count; i++)
            {
                JournalChronicleEntry entry = document.Chronicle[i];
                DateTime local = entry.TimestampUtc.Kind == DateTimeKind.Utc ? entry.TimestampUtc.ToLocalTime() : entry.TimestampUtc;
                builder.Append(local.ToString("yyyy-MM-dd HH:mm"));
                if (!string.IsNullOrWhiteSpace(entry.Category)) builder.Append(" [").Append(entry.Category).Append("]");
                if (!string.IsNullOrWhiteSpace(entry.Source)) builder.Append(" ").Append(entry.Source);
                builder.Append(": ").Append(entry.Text).AppendLine();
            }
            return builder.ToString();
        }

        private void MarkDirty()
        {
            if (_markDirty != null) _markDirty();
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            Color cyanEdge = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color softEdge = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = FramedTexture(new Color(0.015f, 0.09f, 0.125f, 0.90f), cyanEdge);
            _buttonTexture = FramedTexture(new Color(0.035f, 0.17f, 0.22f, 0.88f), softEdge);
            _buttonHoverTexture = FramedTexture(new Color(0.12f, 0.38f, 0.48f, 0.94f), cyanEdge);
            _selectedTexture = FramedTexture(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyanEdge);
            _dangerTexture = FramedTexture(new Color(0.19f, 0.15f, 0.09f, 0.90f), new Color(0.65f, 0.49f, 0.27f, 0.92f));
            _dangerHoverTexture = FramedTexture(new Color(0.34f, 0.23f, 0.10f, 0.96f), new Color(0.86f, 0.63f, 0.30f, 0.98f));
            _textTexture = FramedTexture(new Color(0.018f, 0.055f, 0.068f, 0.92f), softEdge);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(12, 12, 8, 10);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 15;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 1f);

            _sectionStyle = new GUIStyle(GUI.skin.label);
            _sectionStyle.fontSize = 11;
            _sectionStyle.fontStyle = FontStyle.Bold;
            _sectionStyle.normal.textColor = new Color(0.56f, 0.78f, 0.88f, 1f);

            _buttonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture, Color.white);
            _dangerButtonStyle = CreateButtonStyle(_dangerTexture, _dangerHoverTexture, new Color(1f, 0.94f, 0.74f, 1f));
            _closeButtonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture, new Color(0.84f, 0.94f, 1f, 1f));

            _tabStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture, new Color(0.84f, 0.94f, 1f, 1f));
            _tabStyle.fontSize = 11;
            _tabStyle.clipping = TextClipping.Clip;

            _selectedTabStyle = CreateButtonStyle(_selectedTexture, _buttonHoverTexture, new Color(0.88f, 1f, 0.98f, 1f));
            _selectedTabStyle.fontSize = 11;
            _selectedTabStyle.fontStyle = FontStyle.Bold;
            _selectedTabStyle.clipping = TextClipping.Clip;

            _textAreaStyle = new GUIStyle(GUI.skin.textArea);
            _textAreaStyle.fontSize = 13;
            _textAreaStyle.wordWrap = true;
            _textAreaStyle.padding = new RectOffset(9, 9, 8, 8);
            _textAreaStyle.normal.background = _textTexture;
            _textAreaStyle.focused.background = _textTexture;
            _textAreaStyle.hover.background = _textTexture;
            _textAreaStyle.normal.textColor = new Color(0.92f, 0.94f, 0.92f, 1f);
            _textAreaStyle.focused.textColor = Color.white;

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 12;
            _textFieldStyle.normal.background = _textTexture;
            _textFieldStyle.focused.background = _textTexture;
            _textFieldStyle.hover.background = _textTexture;
            _textFieldStyle.normal.textColor = new Color(0.92f, 0.94f, 0.92f, 1f);
            _textFieldStyle.focused.textColor = Color.white;

            _footerStyle = new GUIStyle(GUI.skin.label);
            _footerStyle.fontSize = 10;
            _footerStyle.normal.textColor = new Color(0.63f, 0.73f, 0.74f, 1f);

            _chronicleStyle = new GUIStyle(GUI.skin.label);
            _chronicleStyle.fontSize = 12;
            _chronicleStyle.wordWrap = true;
            _chronicleStyle.normal.textColor = new Color(0.88f, 0.92f, 0.91f, 1f);

            _resizeGripStyle = new GUIStyle(GUI.skin.label);
            _resizeGripStyle.fontSize = 11;
            _resizeGripStyle.alignment = TextAnchor.MiddleCenter;
            _resizeGripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.90f);
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover, Color text)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.margin = new RectOffset(2, 2, 2, 2);
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(6, 6, 2, 2);
            return style;
        }

        private static Texture2D FramedTexture(Color center, Color edge)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    texture.SetPixel(x, y, x == 0 || x == 2 || y == 0 || y == 2 ? edge : center);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
