using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorJournal
{
    internal sealed class JournalWindow
    {
        private const int ChronicleVisibleLimit = 200;

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _tabsContent;
        private Button _chronicleTabButton;
        private readonly List<Button> _tabButtons = new List<Button>();
        private RectTransform _pageRoot;
        private RectTransform _chronicleRoot;
        private RectTransform _chronicleContent;
        private TextMeshProUGUI _pageFooter;
        private TextMeshProUGUI _chronicleFooter;
        private TMP_InputField _nameInput;
        private TMP_InputField _noteInput;
        private Button _deleteButton;
        private TextMeshProUGUI _deleteLabel;
        private Button _clearButton;
        private TextMeshProUGUI _clearLabel;
        private RetainedPosition _position;

        private JournalDocument _document;
        private Action _markDirty;
        private bool _showChronicle;
        private bool _suppressInput;
        private float _deleteArmedUntil;
        private float _clearArmedUntil;
        private string _tabSignature = string.Empty;
        private int _chronicleCount = -1;
        private string _boundTabId = string.Empty;

        internal bool IsTextInputFocused
        {
            get { return (_nameInput != null && _nameInput.isFocused) || (_noteInput != null && _noteInput.isFocused); }
        }

        internal void Initialize(float storedX, float storedY, float width, float height,
            Action<float, float> persist, Action<float, float> persistSize, Action close, Action reset)
        {
            Dispose();
            width = Mathf.Clamp(width, 520f, Mathf.Max(520f, Screen.width - 20f));
            height = Mathf.Clamp(height, 360f, Mathf.Max(360f, Screen.height - 20f));

            _root = RetainedUiKit.CreateCanvas("ErenshorJournalCanvas", 520);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("JournalPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, width, height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            CanvasGroup group = _panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            BuildHeader(close, reset);
            BuildTabs();
            BuildPage();
            BuildChronicle();

            _position = new RetainedPosition(storedX, storedY, 0.5f, 0.5f, persist);
            _position.Resolve(_panel);
            RetainedUiKit.AddResizeGrip("ResizeGrip", _panel, _panel, 16f, new Vector2(520f, 360f), persistSize);
            _root.SetActive(false);
        }

        private void BuildHeader(Action close, Action reset)
        {
            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, 32f);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);

            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "ERENSHOR JOURNAL", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 10f, 0f, 72f, 0f);

            Button resetButton = RetainedUiKit.AddButton("Reset", header, "R", reset, 28f, 24f, false);
            RectTransform rr = resetButton.GetComponent<RectTransform>();
            RemoveLayout(rr);
            rr.anchorMin = rr.anchorMax = new Vector2(1f, 0.5f);
            rr.pivot = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(-38f, 0f);
            rr.sizeDelta = new Vector2(28f, 24f);

            Button closeButton = RetainedUiKit.AddButton("Close", header, "X", close, 28f, 24f, false);
            RectTransform cr = closeButton.GetComponent<RectTransform>();
            RemoveLayout(cr);
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-6f, 0f);
            cr.sizeDelta = new Vector2(28f, 24f);

            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 72f,
                delegate { if (_position != null) _position.DragCompleted(_panel); });
        }

        private void BuildTabs()
        {
            RectTransform tabs = RetainedUiKit.CreateRect("Tabs", _panel);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(8f, -70f);
            tabs.offsetMax = new Vector2(-8f, -34f);

            RectTransform viewport;
            RectTransform content;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("TabScroll", tabs, true, false, out viewport, out content);
            RetainedUiKit.Stretch(scroll.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tabsContent = content;
        }

        private void BuildPage()
        {
            _pageRoot = RetainedUiKit.CreateRect("PageView", _panel);
            _pageRoot.anchorMin = Vector2.zero;
            _pageRoot.anchorMax = Vector2.one;
            _pageRoot.offsetMin = new Vector2(10f, 25f);
            _pageRoot.offsetMax = new Vector2(-10f, -74f);

            RectTransform row = RetainedUiKit.CreateRect("Actions", _pageRoot);
            RetainedUiKit.AnchorTopStretch(row, 0f, 0f, 0f, 30f);

            _nameInput = RetainedUiKit.AddInputField("TabName", row, string.Empty, false, JournalCore.MaxTabNameLength);
            RectTransform nr = _nameInput.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0f, 0.5f);
            nr.offsetMin = new Vector2(0f, 2f); nr.offsetMax = new Vector2(-220f, -2f);
            _nameInput.onValueChanged.AddListener(delegate(string value) { OnNameChanged(value); });

            AddFixedButton(row, "Timestamp", "Timestamp", -214f, 70f, delegate { InsertTimestamp(); }, false);
            AddFixedButton(row, "Copy", "Copy", -140f, 46f, delegate { CopySelected(); }, false);
            _deleteButton = AddFixedButton(row, "Delete", "Delete", -90f, 64f, delegate { DeleteSelected(); }, true);
            _deleteLabel = _deleteButton.GetComponentInChildren<TextMeshProUGUI>();

            _noteInput = RetainedUiKit.AddInputField("Note", _pageRoot, string.Empty, true, 0);
            RectTransform note = _noteInput.GetComponent<RectTransform>();
            note.anchorMin = Vector2.zero;
            note.anchorMax = Vector2.one;
            note.offsetMin = new Vector2(0f, 24f);
            note.offsetMax = new Vector2(0f, -34f);
            _noteInput.onValueChanged.AddListener(delegate(string value) { OnNoteChanged(value); });

            _pageFooter = RetainedUiKit.AddLabel("Footer", _pageRoot, string.Empty, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _pageFooter.rectTransform.anchorMin = new Vector2(0f, 0f);
            _pageFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
            _pageFooter.rectTransform.pivot = new Vector2(0.5f, 0f);
            _pageFooter.rectTransform.offsetMin = new Vector2(0f, 0f);
            _pageFooter.rectTransform.offsetMax = new Vector2(0f, 20f);
        }

        private void BuildChronicle()
        {
            _chronicleRoot = RetainedUiKit.CreateRect("ChronicleView", _panel);
            _chronicleRoot.anchorMin = Vector2.zero;
            _chronicleRoot.anchorMax = Vector2.one;
            _chronicleRoot.offsetMin = new Vector2(10f, 25f);
            _chronicleRoot.offsetMax = new Vector2(-10f, -74f);

            RectTransform row = RetainedUiKit.CreateRect("Actions", _chronicleRoot);
            RetainedUiKit.AnchorTopStretch(row, 0f, 0f, 0f, 30f);
            TextMeshProUGUI heading = RetainedUiKit.AddLabel("Heading", row, "CHRONICLE", 12f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            heading.rectTransform.anchorMin = Vector2.zero; heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = Vector2.zero; heading.rectTransform.offsetMax = new Vector2(-120f, 0f);
            AddFixedButton(row, "Copy", "Copy", -116f, 48f, delegate { CopyChronicle(); }, false);
            _clearButton = AddFixedButton(row, "Clear", "Clear", -64f, 64f, delegate { ClearChronicle(); }, true);
            _clearLabel = _clearButton.GetComponentInChildren<TextMeshProUGUI>();

            RectTransform viewport;
            RectTransform content;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("ChronicleScroll", _chronicleRoot, false, true, out viewport, out content);
            RectTransform sr = scroll.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(0f, 24f); sr.offsetMax = new Vector2(0f, -34f);
            _chronicleContent = RetainedUiKit.AddVerticalContent("Rows", viewport, 7f, 4);
            scroll.content = _chronicleContent;

            _chronicleFooter = RetainedUiKit.AddLabel("Footer", _chronicleRoot, string.Empty, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _chronicleFooter.rectTransform.anchorMin = new Vector2(0f, 0f);
            _chronicleFooter.rectTransform.anchorMax = new Vector2(1f, 0f);
            _chronicleFooter.rectTransform.pivot = new Vector2(0.5f, 0f);
            _chronicleFooter.rectTransform.offsetMin = new Vector2(0f, 0f);
            _chronicleFooter.rectTransform.offsetMax = new Vector2(0f, 20f);
        }

        internal void Tick(bool visible, JournalDocument document, Action markDirty)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);

            _document = document;
            _markDirty = markDirty;
            if (_document == null) return;
            JournalCore.Normalize(_document);

            string signature = BuildTabSignature();
            if (!string.Equals(signature, _tabSignature, StringComparison.Ordinal))
            {
                _tabSignature = signature;
                RebuildTabs();
            }

            RefreshTabVisuals();
            BindSelectedTab();
            if (_chronicleCount != _document.Chronicle.Count)
            {
                _chronicleCount = _document.Chronicle.Count;
                RebuildChronicleRows();
            }

            if (_deleteLabel != null) _deleteLabel.text = Time.unscaledTime < _deleteArmedUntil ? "Confirm" : "Delete";
            if (_clearLabel != null) _clearLabel.text = Time.unscaledTime < _clearArmedUntil ? "Confirm" : "Clear";
            _pageRoot.gameObject.SetActive(!_showChronicle);
            _chronicleRoot.gameObject.SetActive(_showChronicle);
        }

        internal void ResetPosition()
        {
            if (_position != null) _position.Reset(_panel);
        }

        internal void ResetTransientState()
        {
            _document = null;
            _markDirty = null;
            _showChronicle = false;
            _suppressInput = false;
            _deleteArmedUntil = 0f;
            _clearArmedUntil = 0f;
            _tabSignature = string.Empty;
            _chronicleCount = -1;
            _boundTabId = string.Empty;
            _suppressInput = true;
            if (_nameInput != null) _nameInput.text = string.Empty;
            if (_noteInput != null) _noteInput.text = string.Empty;
            _suppressInput = false;
        }

        internal void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null;
            _tabsContent = null;
            _chronicleTabButton = null;
            _tabButtons.Clear();
            _pageRoot = null;
            _chronicleRoot = null;
            _chronicleContent = null;
            _nameInput = null;
            _noteInput = null;
            _document = null;
            _markDirty = null;
            _position = null;
            _tabSignature = string.Empty;
            _chronicleCount = -1;
            _boundTabId = string.Empty;
        }

        private string BuildTabSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_document.Tabs.Count);
            for (int i = 0; i < _document.Tabs.Count; i++)
            {
                JournalTab tab = _document.Tabs[i];
                sb.Append('|').Append(tab == null ? "" : tab.Id);
            }
            return sb.ToString();
        }

        private void RebuildTabs()
        {
            RetainedUiKit.ClearChildren(_tabsContent);
            _chronicleTabButton = RetainedUiKit.AddButton("Chronicle", _tabsContent, "Chronicle", delegate { SelectChronicle(); }, 92f, 28f, false);
            _tabButtons.Clear();
            for (int i = 0; i < _document.Tabs.Count; i++)
            {
                int index = i;
                JournalTab tab = _document.Tabs[i];
                Button b = RetainedUiKit.AddButton("Tab" + i.ToString(), _tabsContent, tab == null ? "Untitled" : tab.Name,
                    delegate { SelectTab(index); }, 92f, 28f, false);
                _tabButtons.Add(b);
            }
            RetainedUiKit.AddButton("AddTab", _tabsContent, "+", delegate { AddTab(); }, 30f, 28f, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tabsContent);
        }

        private void RefreshTabVisuals()
        {
            SetSelected(_chronicleTabButton, _showChronicle);
            for (int i = 0; i < _tabButtons.Count && i < _document.Tabs.Count; i++)
            {
                Button button = _tabButtons[i];
                JournalTab tab = _document.Tabs[i];
                if (button != null)
                {
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    string expected = tab == null ? "Untitled" : (tab.Name ?? string.Empty);
                    if (label != null && !string.Equals(label.text, expected, StringComparison.Ordinal))
                        label.text = expected;
                    SetSelected(button, !_showChronicle && i == _document.SelectedTabIndex);
                }
            }
        }

        private void BindSelectedTab()
        {
            if (_document.Tabs.Count == 0) return;
            JournalTab tab = _document.Tabs[_document.SelectedTabIndex];
            if (tab == null) return;
            if (!string.Equals(_boundTabId, tab.Id, StringComparison.Ordinal))
            {
                _boundTabId = tab.Id;
                _suppressInput = true;
                _nameInput.text = tab.Name ?? string.Empty;
                _noteInput.text = tab.Text ?? string.Empty;
                _suppressInput = false;
            }
            else if (!_nameInput.isFocused && !string.Equals(_nameInput.text, tab.Name ?? string.Empty, StringComparison.Ordinal))
            {
                _suppressInput = true; _nameInput.text = tab.Name ?? string.Empty; _suppressInput = false;
            }
            else if (!_noteInput.isFocused && !string.Equals(_noteInput.text, tab.Text ?? string.Empty, StringComparison.Ordinal))
            {
                _suppressInput = true; _noteInput.text = tab.Text ?? string.Empty; _suppressInput = false;
            }
            if (_pageFooter != null) _pageFooter.text = (tab.Text == null ? 0 : tab.Text.Length).ToString() + " characters";
        }

        private void RebuildChronicleRows()
        {
            RetainedUiKit.ClearChildren(_chronicleContent);
            int start = Math.Max(0, _document.Chronicle.Count - ChronicleVisibleLimit);
            if (_document.Chronicle.Count == 0)
            {
                AddChronicleRow("No Chronicle entries yet. Compatible mods can append verified events through JournalApi; normal tabs remain player-owned.");
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
                    AddChronicleRow(prefix + Environment.NewLine + entry.Text);
                }
            }
            if (_chronicleFooter != null)
                _chronicleFooter.text = start > 0
                    ? "Showing latest " + ChronicleVisibleLimit.ToString() + "; older entries remain saved and are included by Copy."
                    : "Append-only integration history; Journal does not infer game events.";
            LayoutRebuilder.ForceRebuildLayoutImmediate(_chronicleContent);
        }

        private void AddChronicleRow(string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Entry", _chronicleContent, value, 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            label.color = RetainedUiKit.Text;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = Mathf.Max(32f, label.preferredHeight + 8f);
        }

        private void SelectChronicle()
        {
            _showChronicle = true;
            _deleteArmedUntil = 0f;
        }

        private void SelectTab(int index)
        {
            if (_document == null || index < 0 || index >= _document.Tabs.Count) return;
            _showChronicle = false;
            _document.SelectedTabIndex = index;
            _deleteArmedUntil = 0f;
            _boundTabId = string.Empty;
            MarkDirty();
        }

        private void AddTab()
        {
            if (_document == null || !JournalCore.AddTab(_document)) return;
            _showChronicle = false;
            _boundTabId = string.Empty;
            MarkDirty();
            _tabSignature = string.Empty;
        }

        private void OnNameChanged(string value)
        {
            if (_suppressInput || _document == null || _showChronicle || _document.Tabs.Count == 0) return;
            JournalTab tab = _document.Tabs[_document.SelectedTabIndex];
            tab.Name = JournalCore.CleanTabName(value);
            MarkDirty();
        }

        private void OnNoteChanged(string value)
        {
            if (_suppressInput || _document == null || _showChronicle || _document.Tabs.Count == 0) return;
            _document.Tabs[_document.SelectedTabIndex].Text = value ?? string.Empty;
            MarkDirty();
        }

        private void InsertTimestamp()
        {
            if (_document == null || _document.Tabs.Count == 0) return;
            JournalTab tab = _document.Tabs[_document.SelectedTabIndex];
            string prefix = string.IsNullOrEmpty(tab.Text) || tab.Text.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : Environment.NewLine;
            tab.Text += prefix + "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "] ";
            _suppressInput = true; _noteInput.text = tab.Text; _suppressInput = false;
            MarkDirty();
        }

        private void CopySelected()
        {
            if (_document == null || _document.Tabs.Count == 0) return;
            GUIUtility.systemCopyBuffer = _document.Tabs[_document.SelectedTabIndex].Text ?? string.Empty;
        }

        private void DeleteSelected()
        {
            if (_document == null) return;
            if (Time.unscaledTime >= _deleteArmedUntil)
            {
                _deleteArmedUntil = Time.unscaledTime + 4f;
                return;
            }
            if (JournalCore.DeleteSelectedTab(_document))
            {
                _deleteArmedUntil = 0f;
                _boundTabId = string.Empty;
                MarkDirty();
                _tabSignature = string.Empty;
            }
        }

        private void CopyChronicle()
        {
            if (_document == null) return;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < _document.Chronicle.Count; i++)
            {
                JournalChronicleEntry entry = _document.Chronicle[i];
                DateTime local = entry.TimestampUtc.Kind == DateTimeKind.Utc ? entry.TimestampUtc.ToLocalTime() : entry.TimestampUtc;
                builder.Append(local.ToString("yyyy-MM-dd HH:mm"));
                if (!string.IsNullOrWhiteSpace(entry.Category)) builder.Append(" [").Append(entry.Category).Append("]");
                if (!string.IsNullOrWhiteSpace(entry.Source)) builder.Append(" ").Append(entry.Source);
                builder.Append(": ").Append(entry.Text).AppendLine();
            }
            GUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void ClearChronicle()
        {
            if (_document == null) return;
            if (Time.unscaledTime >= _clearArmedUntil)
            {
                _clearArmedUntil = Time.unscaledTime + 4f;
                return;
            }
            _document.Chronicle.Clear();
            _clearArmedUntil = 0f;
            _chronicleCount = -1;
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (_markDirty != null) _markDirty();
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = selected ? RetainedUiKit.Selected : RetainedUiKit.Button;
        }

        private static Button AddFixedButton(RectTransform parent, string name, string label, float right, float width, Action action, bool danger)
        {
            Button b = RetainedUiKit.AddButton(name, parent, label, action, width, 26f, danger);
            RectTransform r = b.GetComponent<RectTransform>();
            RemoveLayout(r);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(right, 0f);
            r.sizeDelta = new Vector2(width, 26f);
            return b;
        }

        private static void RemoveLayout(RectTransform rect)
        {
            LayoutElement le = rect.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.DestroyImmediate(le);
        }
    }
}
