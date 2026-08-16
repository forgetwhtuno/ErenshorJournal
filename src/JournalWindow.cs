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
        internal const int CanvasSortOrder = 520;
        internal const float MinimumWidth = 440f;
        internal const float MinimumHeight = 320f;
        private const int ChronicleVisibleLimit = 200;

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _bodyRoot;
        private RectTransform _collapseChevron;
        private GameObject _resizeGripRoot;
        private bool _collapsed;
        private float _expandedHeight;
        private RectTransform _tabsContent;
        private Button _chronicleTabButton;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Button> _chronicleRowButtons = new List<Button>();
        private readonly List<int> _chronicleRowIndices = new List<int>();
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
        private int _selectedChronicleIndex = -1;
        private string _boundTabId = string.Empty;
        private float _preferredWidth;
        private float _preferredHeight;
        private Action<float, float> _persistSize;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        internal bool IsTextInputFocused
        {
            get { return (_nameInput != null && _nameInput.isFocused) || (_noteInput != null && _noteInput.isFocused); }
        }

        internal void Initialize(float storedX, float storedY, float width, float height,
            Action<float, float> persist, Action<float, float> persistSize, Action close, Action reset)
        {
            Dispose();
            _preferredWidth = Mathf.Max(MinimumWidth, IsFinite(width) ? width : MinimumWidth);
            _preferredHeight = Mathf.Max(MinimumHeight, IsFinite(height) ? height : MinimumHeight);
            _persistSize = persistSize;
            width = JournalUiLayoutPolicy.ResolvePanelExtent(_preferredWidth, Screen.width, MinimumWidth, 10f);
            height = JournalUiLayoutPolicy.ResolvePanelExtent(_preferredHeight, Screen.height, MinimumHeight, 10f);

            _root = RetainedUiKit.CreateCanvas("ErenshorJournalCanvas", CanvasSortOrder);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("JournalPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, width, height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            CanvasGroup group = _panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            _bodyRoot = RetainedUiKit.CreateRect("Body", _panel);
            RetainedUiKit.Stretch(_bodyRoot, 0f, 0f, 0f, 0f);
            _expandedHeight = height;
            _collapsed = false;

            BuildHeader(close, reset);
            BuildTabs();
            BuildPage();
            BuildChronicle();

            _position = new RetainedPosition(storedX, storedY, 0.5f, 0.5f, persist);
            _position.Resolve(_panel);
            SuiteResizeHandler resize = RetainedUiKit.AddResizeGrip("ResizeGrip", _panel, _panel, 16f, new Vector2(MinimumWidth, MinimumHeight), ResizeCompleted);
            _resizeGripRoot = resize == null ? null : resize.gameObject;
            RetainedUiKit.AddFrame(_panel, 1f);
            UpdateCollapseVisual();
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _root.SetActive(false);
        }

        private void BuildHeader(Action close, Action reset)
        {
            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, SuiteWindowChromePolicy.HeaderHeight);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);

            AddCollapseButton(header);

            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "JOURNAL", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 40f, 0f, 72f, 0f);

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

            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 36f, 72f,
                delegate
                {
                    if (_position == null) return;
                    if (_collapsed) _position.Clamp(_panel);
                    else _position.DragCompleted(_panel);
                });
        }

        private void BuildTabs()
        {
            RectTransform tabs = RetainedUiKit.CreateRect("Tabs", _bodyRoot);
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
            layout.spacing = 3f;
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
            _pageRoot = RetainedUiKit.CreateRect("PageView", _bodyRoot);
            _pageRoot.anchorMin = Vector2.zero;
            _pageRoot.anchorMax = Vector2.one;
            _pageRoot.offsetMin = new Vector2(10f, 25f);
            _pageRoot.offsetMax = new Vector2(-10f, -74f);

            RectTransform row = RetainedUiKit.CreateRect("Actions", _pageRoot);
            RetainedUiKit.AnchorTopStretch(row, 0f, 0f, 0f, 30f);

            _nameInput = RetainedUiKit.AddInputField("TabName", row, string.Empty, false, JournalCore.MaxTabNameLength);
            RectTransform nr = _nameInput.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0f, 0.5f);
            nr.offsetMin = new Vector2(0f, 2f);
            nr.offsetMax = new Vector2(-JournalUiLayoutPolicy.NameInputRightInset, -2f);
            _nameInput.onValueChanged.AddListener(delegate(string value) { OnNameChanged(value); });

            // This is intentionally a MANUAL note-body helper, not a Chronicle/history action. The
            // old "New Entry" label was ambiguous because it actually appended a timestamp inside
            // the currently selected player note.
            AddFixedButton(row, "NewEntry", "Add Time", JournalUiLayoutPolicy.NewEntryRight,
                JournalUiLayoutPolicy.NewEntryWidth, delegate { StartNewEntry(); }, false);
            AddFixedButton(row, "Copy", "Copy", JournalUiLayoutPolicy.CopyRight,
                JournalUiLayoutPolicy.CopyWidth, delegate { CopySelected(); }, false);
            _deleteButton = AddFixedButton(row, "Delete", "Delete", JournalUiLayoutPolicy.DeleteRight,
                JournalUiLayoutPolicy.DeleteWidth, delegate { DeleteSelected(); }, true);
            _deleteLabel = _deleteButton.GetComponentInChildren<TextMeshProUGUI>();

            _noteInput = RetainedUiKit.AddInputField("Note", _pageRoot, string.Empty, true, 0);
            TextMeshProUGUI emptyState = _noteInput.placeholder as TextMeshProUGUI;
            if (emptyState != null) emptyState.text = "Write a note...";
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
            _chronicleRoot = RetainedUiKit.CreateRect("ChronicleView", _bodyRoot);
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
            _chronicleContent = RetainedUiKit.AddVerticalContent("Rows", viewport, 6f, 4);
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
            ApplyPreferredSizeForScreen();

            _document = document;
            _markDirty = markDirty;
            if (_collapsed) return;
            if (_document == null) return;
            JournalCore.Normalize(_document);

            string signature = BuildTabSignature();
            if (SuiteWindowChromePolicy.ShouldRebuildStructure(_tabSignature, signature))
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

            bool canDelete = _document.Tabs.Count > 1;
            if (_deleteButton != null) _deleteButton.interactable = canDelete;
            if (!canDelete) _deleteArmedUntil = 0f;
            bool canClear = _document.Chronicle.Count > 0;
            if (_clearButton != null) _clearButton.interactable = canClear;
            if (!canClear) _clearArmedUntil = 0f;
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
            _selectedChronicleIndex = -1;
            _chronicleRowButtons.Clear();
            _chronicleRowIndices.Clear();
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
            _bodyRoot = null;
            _collapseChevron = null;
            _resizeGripRoot = null;
            _collapsed = false;
            _expandedHeight = 0f;
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
            _selectedChronicleIndex = -1;
            _chronicleRowButtons.Clear();
            _chronicleRowIndices.Clear();
            _boundTabId = string.Empty;
            _persistSize = null;
            _preferredWidth = 0f;
            _preferredHeight = 0f;
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
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
            _chronicleTabButton = RetainedUiKit.AddButton("Chronicle", _tabsContent, "Chronicle", delegate { SelectChronicle(); },
                JournalUiLayoutPolicy.TabWidthForName("Chronicle"), 26f, false);
            _tabButtons.Clear();
            for (int i = 0; i < _document.Tabs.Count; i++)
            {
                int index = i;
                JournalTab tab = _document.Tabs[i];
                string label = tab == null ? "Untitled" : tab.Name;
                Button b = RetainedUiKit.AddButton("Tab" + i.ToString(), _tabsContent, label,
                    delegate { SelectTab(index); }, JournalUiLayoutPolicy.TabWidthForName(label), 26f, false);
                _tabButtons.Add(b);
            }
            RetainedUiKit.AddButton("AddTab", _tabsContent, "+", delegate { AddTab(); }, 30f, 26f, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tabsContent);
        }

        private void RefreshTabVisuals()
        {
            SetSelected(_chronicleTabButton, _showChronicle);
            bool layoutChanged = false;
            for (int i = 0; i < _tabButtons.Count && i < _document.Tabs.Count; i++)
            {
                Button button = _tabButtons[i];
                JournalTab tab = _document.Tabs[i];
                if (button != null)
                {
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    string expected = tab == null ? "Untitled" : (tab.Name ?? string.Empty);
                    if (label != null && !string.Equals(label.text, expected, StringComparison.Ordinal))
                    {
                        label.text = expected;
                        LayoutElement element = button.GetComponent<LayoutElement>();
                        if (element != null) element.preferredWidth = JournalUiLayoutPolicy.TabWidthForName(expected);
                        layoutChanged = true;
                    }
                    SetSelected(button, !_showChronicle && i == _document.SelectedTabIndex);
                }
            }
            if (layoutChanged && _tabsContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_tabsContent);
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
            _chronicleRowButtons.Clear();
            _chronicleRowIndices.Clear();
            int start = Math.Max(0, _document.Chronicle.Count - ChronicleVisibleLimit);
            if (_document.Chronicle.Count == 0)
            {
                _selectedChronicleIndex = -1;
                AddChronicleEmptyRow("No Chronicle entries yet. Meaningful progression shared by compatible systems will appear here as separate history entries; your normal tabs remain private notes.");
            }
            else
            {
                if (_selectedChronicleIndex < start || _selectedChronicleIndex >= _document.Chronicle.Count)
                    _selectedChronicleIndex = _document.Chronicle.Count - 1;
                for (int i = start; i < _document.Chronicle.Count; i++) AddChronicleRow(i, _document.Chronicle[i]);
            }
            RefreshChronicleSelection();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_chronicleContent);
        }

        private void AddChronicleEmptyRow(string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Empty", _chronicleContent, value, 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            label.color = RetainedUiKit.Muted;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 38f;
            le.preferredHeight = Mathf.Max(38f, label.preferredHeight + 8f);
        }

        private void AddChronicleRow(int index, JournalChronicleEntry entry)
        {
            DateTime local = entry.TimestampUtc.Kind == DateTimeKind.Utc ? entry.TimestampUtc.ToLocalTime() : entry.TimestampUtc;
            string title = GetChronicleDisplayTitle(entry);
            string meta = local.ToString("MMM d, yyyy h:mm tt") + "  •  " + title;
            string provenance = string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.Source)) provenance = entry.Source;
            if (!string.IsNullOrWhiteSpace(entry.Category)) provenance += (provenance.Length == 0 ? string.Empty : "  ·  ") + entry.Category;
            string labelText = meta;
            if (provenance.Length > 0) labelText += Environment.NewLine + provenance;
            labelText += Environment.NewLine + entry.Text;

            int captured = index;
            Button button = RetainedUiKit.AddButton("Entry" + index.ToString(), _chronicleContent, labelText,
                delegate { SelectChronicleEntry(captured); }, 0f, 52f, false);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.TopLeft;
                label.enableWordWrapping = true;
                LayoutElement le = button.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = 52f;
                    le.preferredHeight = Mathf.Max(52f, label.preferredHeight + 10f);
                }
            }
            _chronicleRowButtons.Add(button);
            _chronicleRowIndices.Add(index);
        }

        private void SelectChronicleEntry(int index)
        {
            if (_document == null || index < 0 || index >= _document.Chronicle.Count) return;
            _selectedChronicleIndex = index;
            RefreshChronicleSelection();
        }

        private void RefreshChronicleSelection()
        {
            for (int i = 0; i < _chronicleRowButtons.Count && i < _chronicleRowIndices.Count; i++)
                SetSelected(_chronicleRowButtons[i], _chronicleRowIndices[i] == _selectedChronicleIndex);

            if (_chronicleFooter == null) return;
            if (_document == null || _document.Chronicle.Count == 0 || _selectedChronicleIndex < 0 || _selectedChronicleIndex >= _document.Chronicle.Count)
            {
                _chronicleFooter.text = "Chronicle keeps local structured history separate from your manual notes.";
                return;
            }

            JournalChronicleEntry selected = _document.Chronicle[_selectedChronicleIndex];
            string title = GetChronicleDisplayTitle(selected);
            string source = string.IsNullOrWhiteSpace(selected.Source) ? string.Empty : " · " + selected.Source;
            int hidden = Math.Max(0, _document.Chronicle.Count - ChronicleVisibleLimit);
            string older = hidden > 0 ? " · " + hidden.ToString() + " older saved" : string.Empty;
            _chronicleFooter.text = "Selected: " + title + source + older + ". Copy exports the full Chronicle.";
        }

        private void SelectChronicle()
        {
            _showChronicle = true;
            _deleteArmedUntil = 0f;
            _clearArmedUntil = 0f;
        }

        private void SelectTab(int index)
        {
            if (_document == null || index < 0 || index >= _document.Tabs.Count) return;
            _showChronicle = false;
            _document.SelectedTabIndex = index;
            _deleteArmedUntil = 0f;
            _clearArmedUntil = 0f;
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

        private void StartNewEntry()
        {
            if (_document == null || _document.Tabs.Count == 0) return;
            JournalTab tab = _document.Tabs[_document.SelectedTabIndex];
            tab.Text = JournalEntryPolicy.AppendTimestampMarker(tab.Text, DateTime.Now, Environment.NewLine);
            _suppressInput = true;
            _noteInput.text = tab.Text;
            _suppressInput = false;
            _noteInput.ActivateInputField();
            _noteInput.caretPosition = _noteInput.text == null ? 0 : _noteInput.text.Length;
            MarkDirty();
        }

        private void CopySelected()
        {
            if (_document == null || _document.Tabs.Count == 0) return;
            GUIUtility.systemCopyBuffer = _document.Tabs[_document.SelectedTabIndex].Text ?? string.Empty;
        }

        private void DeleteSelected()
        {
            if (_document == null || _document.Tabs.Count <= 1) return;
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
                builder.Append(" — ").Append(GetChronicleDisplayTitle(entry));
                if (!string.IsNullOrWhiteSpace(entry.Category)) builder.Append(" [").Append(entry.Category).Append("]");
                if (!string.IsNullOrWhiteSpace(entry.Source)) builder.Append(" ").Append(entry.Source);
                builder.Append(": ").Append(entry.Text).AppendLine();
            }
            GUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void ClearChronicle()
        {
            if (_document == null || _document.Chronicle.Count == 0) return;
            if (Time.unscaledTime >= _clearArmedUntil)
            {
                _clearArmedUntil = Time.unscaledTime + 4f;
                return;
            }
            _document.Chronicle.Clear();
            _clearArmedUntil = 0f;
            _selectedChronicleIndex = -1;
            _chronicleCount = -1;
            MarkDirty();
        }

        private void ApplyPreferredSizeForScreen()
        {
            if (_panel == null) return;
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height) return;

            float width = JournalUiLayoutPolicy.ResolvePanelExtent(_preferredWidth, Screen.width, MinimumWidth, 10f);
            float expanded = JournalUiLayoutPolicy.ResolvePanelExtent(_preferredHeight, Screen.height, MinimumHeight, 10f);
            _expandedHeight = expanded;
            float oldHeight = _panel.rect.height;
            float oldTop = _panel.anchoredPosition.y + oldHeight;
            float displayHeight = _collapsed ? SuiteWindowChromePolicy.CollapsedHeight : expanded;
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, displayHeight);
            if (_collapsed)
            {
                Vector2 p = _panel.anchoredPosition;
                p.y = oldTop - displayHeight;
                _panel.anchoredPosition = p;
            }
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_position != null)
            {
                if (_collapsed) _position.Clamp(_panel);
                else _position.Resolve(_panel);
                _position.Clamp(_panel);
            }
        }

        private void ResizeCompleted(float width, float height)
        {
            if (!IsFinite(width) || !IsFinite(height)) return;
            _preferredWidth = Mathf.Max(MinimumWidth, width);
            _preferredHeight = Mathf.Max(MinimumHeight, height);
            _expandedHeight = _preferredHeight;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            if (_position != null) _position.DragCompleted(_panel);
            if (_persistSize != null) _persistSize(_preferredWidth, _preferredHeight);
        }

        private void AddCollapseButton(RectTransform header)
        {
            Button button = RetainedUiKit.AddButton("Collapse", header, "", ToggleCollapsed, 28f, 24f, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            RemoveLayout(rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
            rect.sizeDelta = new Vector2(28f, 24f);
            _collapseChevron = button.GetComponent<RectTransform>();
            RetainedUiKit.AddVerticalChevron(_collapseChevron, true);
        }

        private void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            if (_panel == null || _collapsed == collapsed) return;

            float oldHeight = _panel.rect.height;
            float oldTop = _panel.anchoredPosition.y + oldHeight;
            if (collapsed && _expandedHeight < MinimumHeight)
            {
                _expandedHeight = Mathf.Max(MinimumHeight, oldHeight);
            }

            _collapsed = collapsed;
            float desired = SuiteWindowChromePolicy.ResolveDisplayHeight(_collapsed, _expandedHeight, MinimumHeight);
            if (!_collapsed) desired = Mathf.Min(desired, Mathf.Max(SuiteWindowChromePolicy.CollapsedHeight, Screen.height - 20f));
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desired);

            Vector2 position = _panel.anchoredPosition;
            position.y = SuiteWindowChromePolicy.PreserveTopBottomY(position.y, oldHeight, desired);
            _panel.anchoredPosition = position;

            if (_bodyRoot != null) _bodyRoot.gameObject.SetActive(!_collapsed);
            if (_resizeGripRoot != null) _resizeGripRoot.SetActive(!_collapsed);
            UpdateCollapseVisual();

            if (_position != null)
            {
                _position.Clamp(_panel);
                if (!_collapsed) _position.DragCompleted(_panel);
            }
        }

        private void UpdateCollapseVisual()
        {
            if (_collapseChevron == null) return;
            for (int i = _collapseChevron.childCount - 1; i >= 0; i--)
                if (_collapseChevron.GetChild(i).name == "Chevron") UnityEngine.Object.Destroy(_collapseChevron.GetChild(i).gameObject);
            // Expanded means click to collapse upward; collapsed means click to expand down.
            RetainedUiKit.AddVerticalChevron(_collapseChevron, !_collapsed);
        }

        private static string GetChronicleDisplayTitle(JournalChronicleEntry entry)
        {
            if (entry == null) return "Chronicle Entry";
            string title = JournalCore.CleanChronicleLabel(entry.Title, JournalCore.MaxChronicleTitleLength);
            return title.Length > 0 ? title : JournalCore.ResolveChronicleTitle(entry.Source, entry.Category, entry.Text);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
