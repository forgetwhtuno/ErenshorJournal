using System;
using System.IO;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;

namespace ErenshorJournal
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "A small local, freeform player notebook with an optional Chronicle sink for verified events from other mods.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Harmony)]
    public sealed class ErenshorJournalPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.journal";
        internal const string PluginName = "Erenshor Journal";
        internal const string PluginVersion = "0.1.3";

        internal static ErenshorJournalPlugin Instance;
        private Harmony _harmony;
        private bool _forcedPlayerTyping;

        private JournalSettings _settings;
        private JournalConfigEntry<float> _launcherX;
        private JournalConfigEntry<float> _launcherY;
        private JournalConfigEntry<float> _windowX;
        private JournalConfigEntry<float> _windowY;
        private JournalConfigEntry<float> _windowWidth;
        private JournalConfigEntry<float> _windowHeight;

        private JournalStore _store;
        private JournalDocument _document;
        private JournalWindow _window;
        private JournalLauncher _launcher;
        private Rect _windowRect;
        private Rect _launcherRect;
        private bool _open;
        private bool _dirty;
        private float _saveAfter;
        private bool _launcherDirty;
        private float _launcherSaveAfter;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;

        private void Awake()
        {
            Instance = this;
            _settings = new JournalSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            string dataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorJournal");
            _store = new JournalStore(Path.Combine(dataDirectory, "journal.dat"));
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning)) Logging.LogWarning("Erenshor Journal recovered from unreadable local data. " + warning);

            _window = new JournalWindow();
            _launcher = new JournalLauncher();
            _windowRect = ResolveInitialRect();
            _launcherRect = ResolveInitialLauncherRect();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logging.LogInfo("Erenshor Journal " + PluginVersion + " loaded. Use the draggable Journal UI button to open or close it. Journal does not register a global hotkey. Notes remain local and are never logged or networked.");
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new JournalConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new JournalConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _windowX = new JournalConfigEntry<float>(delegate { return _settings.WindowX; }, delegate(float v) { _settings.WindowX = v; });
            _windowY = new JournalConfigEntry<float>(delegate { return _settings.WindowY; }, delegate(float v) { _settings.WindowY = v; });
            _windowWidth = new JournalConfigEntry<float>(delegate { return _settings.WindowWidth; }, delegate(float v) { _settings.WindowWidth = v; });
            _windowHeight = new JournalConfigEntry<float>(delegate { return _settings.WindowHeight; }, delegate(float v) { _settings.WindowHeight = v; });
        }

        private void Update()
        {
            try
            {
                PendingChronicleEntry pending;
                bool appended = false;
                while (JournalApi.TryDequeue(out pending))
                {
                    JournalCore.AppendChronicle(_document, pending.Source, pending.Category, pending.Text, pending.TimestampUtc);
                    appended = true;
                }
                if (appended) MarkDirty();

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
                if (_launcherDirty && Time.unscaledTime >= _launcherSaveAfter) PersistLauncherRect();
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Journal update failed: " + ex);
            }
        }

        private void OnGUI()
        {
            try
            {
                bool textFocused = false;
                if (_open && _window != null && _document != null)
                {
                    _windowRect = ClampRect(_window.Draw(_windowRect, _document, MarkDirty));
                    textFocused = _window.IsTextInputFocused;
                    if (_window.RequestClose) CloseJournal();
                }
                UpdatePlayerTyping(textFocused);

                if (_launcher != null)
                {
                    Rect previous = _launcherRect;
                    _launcherRect = ClampLauncherRect(_launcher.Draw(_launcherRect, _open));
                    if (!RectsNearlyEqual(previous, _launcherRect)) MarkLauncherDirty();
                    if (_launcher.RequestToggle) ToggleJournal();
                }
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Journal UI failed: " + ex);
                if (_open) CloseJournal();
            }
        }

        // Only ever forces GameData.PlayerTyping on a transition we own, so we never clobber the
        // native chat box (or another mod) that might independently be setting the same flag.
        private void UpdatePlayerTyping(bool wantsTyping)
        {
            if (wantsTyping && !_forcedPlayerTyping)
            {
                GameData.PlayerTyping = true;
                _forcedPlayerTyping = true;
            }
            else if (!wantsTyping && _forcedPlayerTyping)
            {
                GameData.PlayerTyping = false;
                _forcedPlayerTyping = false;
            }
        }

        // True while the pointer (already converted to GUI screen-space by the caller) is over
        // the journal window or its launcher button. The click-passthrough Harmony patches below
        // use this so a click on the panel cannot also drop the player's world target or spin
        // the camera.
        internal bool PointerIsOverUi(Vector2 guiPoint)
        {
            if (_open && _windowRect.Contains(guiPoint)) return true;
            if (_launcherRect.Contains(guiPoint)) return true;
            return false;
        }

        private void OnDestroy()
        {
            try { JournalCameraLookPatch.Restore(); } catch { }
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { UpdatePlayerTyping(false); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
            if (Instance == this) Instance = null;
        }

        private void ToggleJournal()
        {
            if (_open) CloseJournal();
            else OpenJournal();
        }

        private void OpenJournal()
        {
            if (_open) return;
            _open = true;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseJournal()
        {
            if (!_open) return;
            _open = false;
            SaveNow();
            PersistWindowRect();
            RestoreCursor();
        }

        private void RestoreCursor()
        {
            Cursor.visible = _cursorVisibleBeforeOpen;
            Cursor.lockState = _cursorLockBeforeOpen;
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveAfter = Time.unscaledTime + 0.8f;
        }

        private void MarkLauncherDirty()
        {
            _launcherDirty = true;
            _launcherSaveAfter = Time.unscaledTime + 0.8f;
        }

        private void SaveNow()
        {
            if (_store == null || _document == null) return;
            if (!_dirty && File.Exists(_store.PathOnDisk)) return;

            try
            {
                _store.Save(_document);
                _dirty = false;
            }
            catch (Exception ex)
            {
                _dirty = true;
                _saveAfter = Time.unscaledTime + 5f;
                Logging.LogError("Erenshor Journal could not save local notes: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private Rect ResolveInitialRect()
        {
            float width = Mathf.Clamp(_windowWidth.Value, 520f, Mathf.Max(520f, Screen.width - 20f));
            float height = Mathf.Clamp(_windowHeight.Value, 360f, Mathf.Max(360f, Screen.height - 20f));
            float x = _windowX.Value < 0f ? (Screen.width - width) * 0.5f : _windowX.Value;
            float y = _windowY.Value < 0f ? (Screen.height - height) * 0.5f : _windowY.Value;
            return ClampRect(new Rect(x, y, width, height));
        }

        private Rect ResolveInitialLauncherRect()
        {
            float x = _launcherX.Value < 0f ? Mathf.Max(0f, Screen.width - JournalLauncher.Width - 18f) : _launcherX.Value;
            float y = _launcherY.Value < 0f ? Mathf.Min(Mathf.Max(8f, 128f), Mathf.Max(0f, Screen.height - JournalLauncher.Height)) : _launcherY.Value;
            return ClampLauncherRect(new Rect(x, y, JournalLauncher.Width, JournalLauncher.Height));
        }

        private static Rect ClampRect(Rect rect)
        {
            float maxWidth = Mathf.Max(520f, Screen.width - 20f);
            float maxHeight = Mathf.Max(360f, Screen.height - 20f);
            rect.width = Mathf.Clamp(rect.width, 520f, maxWidth);
            rect.height = Mathf.Clamp(rect.height, 360f, maxHeight);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private static Rect ClampLauncherRect(Rect rect)
        {
            rect.width = JournalLauncher.Width;
            rect.height = JournalLauncher.Height;
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private void PersistWindowRect()
        {
            if (_windowX == null || _windowY == null || _windowWidth == null || _windowHeight == null) return;
            Rect rect = ClampRect(_windowRect);

            _windowX.Value = rect.x;
            _windowY.Value = rect.y;
            _windowWidth.Value = rect.width;
            _windowHeight.Value = rect.height;
            Config.Save();
        }

        private void PersistLauncherRect()
        {
            if (_launcherX == null || _launcherY == null) return;
            Rect rect = ClampLauncherRect(_launcherRect);

            _launcherX.Value = rect.x;
            _launcherY.Value = rect.y;
            Config.Save();
            _launcherDirty = false;
        }

        private static bool RectsNearlyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.25f &&
                   Mathf.Abs(a.y - b.y) < 0.25f &&
                   Mathf.Abs(a.width - b.width) < 0.25f &&
                   Mathf.Abs(a.height - b.height) < 0.25f;
        }
    }

    // IMGUI doesn't own the raw click Erenshor reads here, so a click on the Journal window or
    // its launcher would otherwise also affect the world (deselect target, move camera). Same
    // pattern as Erenshor-PvP's PvpPanelLeftClickPatch / Erenshor Crafting Expanded's
    // CraftingPanelLeftClickPatch.
    [HarmonyPatch(typeof(PlayerControl), "LeftClick")]
    internal static class JournalPanelLeftClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            try
            {
                if (ErenshorJournalPlugin.Instance == null) return true;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return !ErenshorJournalPlugin.Instance.PointerIsOverUi(mouse);
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(csMouseOrbit), "LateUpdate")]
    internal static class JournalCameraLookPatch
    {
        private static csMouseOrbit _muted;
        private static float _mutedX;
        private static float _mutedY;

        internal static void Restore()
        {
            csMouseOrbit orbit = _muted;
            _muted = null;
            if (orbit == null) return;
            try { orbit.xSpeed = _mutedX; orbit.ySpeed = _mutedY; } catch { }
        }

        [HarmonyPrefix]
        private static void Prefix(csMouseOrbit __instance)
        {
            Restore();
            try
            {
                if (__instance == null || ErenshorJournalPlugin.Instance == null) return;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!ErenshorJournalPlugin.Instance.PointerIsOverUi(mouse)) return;
                _mutedX = __instance.xSpeed;
                _mutedY = __instance.ySpeed;
                __instance.xSpeed = 0f;
                __instance.ySpeed = 0f;
                _muted = __instance;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void Postfix() { Restore(); }
    }
}
