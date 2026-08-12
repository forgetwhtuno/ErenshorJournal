using UnityEngine;

namespace ErenshorJournal
{
    internal sealed class JournalLauncher
    {
        private const int WindowId = 0x45524A4C;
        internal const float Width = 112f;
        internal const float Height = 34f;

        private bool _journalOpen;
        private bool _requestToggle;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _buttonOpenTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonOpenStyle;
        private GUIStyle _gripStyle;

        internal bool RequestToggle
        {
            get { return _requestToggle; }
        }

        internal Rect Draw(Rect rect, bool journalOpen)
        {
            EnsureStyles();
            _journalOpen = journalOpen;
            _requestToggle = false;

            int previousDepth = GUI.depth;
            try
            {
                GUI.depth = -70;
                return GUI.Window(WindowId, rect, DrawContents, GUIContent.none, _windowStyle);
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        internal void Dispose()
        {
            DestroyTexture(ref _panelTexture);
            DestroyTexture(ref _buttonTexture);
            DestroyTexture(ref _buttonHoverTexture);
            DestroyTexture(ref _buttonOpenTexture);
            _windowStyle = null;
            _buttonStyle = null;
            _buttonOpenStyle = null;
            _gripStyle = null;
        }

        private void DrawContents(int id)
        {
            GUI.Label(new Rect(3f, 5f, 14f, 24f), "||", _gripStyle);
            GUIStyle style = _journalOpen ? _buttonOpenStyle : _buttonStyle;
            if (GUI.Button(new Rect(18f, 4f, Width - 22f, 26f), "JOURNAL", style))
                _requestToggle = true;

            // A narrow grip owns dragging. The action surface remains a pure
            // open/close toggle so clicking it never also moves the launcher.
            GUI.DragWindow(new Rect(0f, 0f, 18f, Height));
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            Color cyanEdge = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color softEdge = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = FramedTexture(new Color(0.015f, 0.09f, 0.125f, 0.78f), cyanEdge);
            _buttonTexture = FramedTexture(new Color(0.035f, 0.17f, 0.22f, 0.86f), softEdge);
            _buttonHoverTexture = FramedTexture(new Color(0.12f, 0.38f, 0.48f, 0.94f), cyanEdge);
            _buttonOpenTexture = FramedTexture(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyanEdge);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _buttonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture);
            _buttonOpenStyle = CreateButtonStyle(_buttonOpenTexture, _buttonHoverTexture);
            _buttonOpenStyle.normal.textColor = new Color(0.88f, 1f, 0.98f, 1f);

            _gripStyle = new GUIStyle(GUI.skin.label);
            _gripStyle.fontSize = 10;
            _gripStyle.fontStyle = FontStyle.Bold;
            _gripStyle.alignment = TextAnchor.MiddleCenter;
            _gripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.95f);
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = new Color(0.84f, 0.94f, 1f, 1f);
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.fontSize = 11;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(4, 4, 0, 0);
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
            Object.Destroy(texture);
            texture = null;
        }
    }
}
