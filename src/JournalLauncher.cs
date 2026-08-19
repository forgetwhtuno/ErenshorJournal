using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorJournal
{
    internal sealed class JournalLauncher
    {
        internal const float Width = StandaloneLauncherVisual.Width;
        internal const float Height = StandaloneLauncherVisual.Height;

        private GameObject _root;
        private RectTransform _panel;
        private TextMeshProUGUI _label;
        private GameObject _openAccent;
        private RetainedPosition _position;
        private Action _toggle;

        internal void Initialize(float storedX, float storedY, Action<float, float> persist, Action toggle)
        {
            Dispose();
            _toggle = toggle;
            _root = RetainedUiKit.CreateCanvas("ErenshorJournalLauncherCanvas", 510);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("JournalLauncher", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, Width, Height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);

            // Fixed (non-stretch) anchors, matching what StandaloneLauncherVisual.StyleGrip below
            // assumes: it sets grip.sizeDelta = (GripWidth, Height) as an ABSOLUTE size. A vertically
            // stretched anchor (anchorMin.y=0, anchorMax.y=1) treats sizeDelta.y as an ADDITIVE offset
            // on top of the already-parent-matched height instead, which previously doubled the grip's
            // height to 64 and, with pivot.y=0, pushed the extra 32px entirely above the launcher.
            RectTransform grip = RetainedUiKit.CreateRect("DragGrip", _panel);
            RetainedUiKit.AnchorBottomLeft(grip, 0f, 0f, StandaloneLauncherVisual.GripWidth, Height);
            RetainedUiKit.AddImage(grip, RetainedUiKit.Header);
            TextMeshProUGUI diamond = RetainedUiKit.AddLabel("GripLabel", grip, "◇", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            RetainedUiKit.Stretch(diamond.rectTransform, 0f, 0f, 0f, 0f);
            diamond.gameObject.SetActive(false);
            StandaloneLauncherVisual.StyleGrip(grip);

            Button button = RetainedUiKit.AddButton("OpenJournal", _panel, "JOURNAL", delegate { if (_toggle != null) _toggle(); }, Width - 20f, Height, false);
            RectTransform br = button.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.zero;
            br.pivot = Vector2.zero;
            br.anchoredPosition = new Vector2(20f, 0f);
            br.sizeDelta = new Vector2(Width - 20f, Height);
            LayoutElement le = br.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.DestroyImmediate(le);
            _label = button.GetComponentInChildren<TextMeshProUGUI>();
            StandaloneLauncherVisual.StyleButton(button, _label);
            StandaloneLauncherVisual.StyleRoot(_panel);
            _openAccent = StandaloneLauncherVisual.AddOpenAccent(_panel);

            _position = new RetainedPosition(storedX, storedY,
                StandaloneLauncherColumnPolicy.DefaultX(),
                StandaloneLauncherColumnPolicy.DefaultY(StandaloneLauncherColumnPolicy.SlotIndex),
                persist);
            SuiteDragHandler drag = grip.gameObject.AddComponent<SuiteDragHandler>();
            drag.Target = _panel;
            drag.OnDragCompleted = delegate { if (_position != null) _position.DragCompleted(_panel); };
            _position.Resolve(_panel);
            _root.SetActive(false);
        }

        internal void Tick(bool visible, bool open)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);
            // The module name stays stable regardless of open state; open/active is communicated
            // structurally (see StandaloneLauncherVisual.AddOpenAccent), matching the other two
            // Forgotten Roads standalone launchers instead of a text-only "[OPEN]" suffix.
            if (_label != null) _label.text = "JOURNAL";
            if (_openAccent != null) _openAccent.SetActive(open);
        }

        internal void ResetPosition()
        {
            if (_position != null) _position.Reset(_panel);
        }

        internal void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null;
            _label = null;
            _openAccent = null;
            _position = null;
            _toggle = null;
        }
    }
}
