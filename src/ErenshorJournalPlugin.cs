using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace ErenshorJournal
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Erenshor.exe")]
    public sealed class ErenshorJournalPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.journal";
        internal const string PluginName = "Erenshor Journal";
        internal const string PluginVersion = "0.1.2";

        private ConfigEntry<float> _launcherX;
        private ConfigEntry<float> _launcherY;
        private ConfigEntry<float> _windowX;
        private ConfigEntry<float> _windowY;
        private ConfigEntry<float> _windowWidth;
        private ConfigEntry<float> _windowHeight;

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
            _launcherX = Config.Bind("UI", "LauncherX", -1f,
                "Saved launcher X position. -1 places it near the right side of the screen on first use.");
            _launcherY = Config.Bind("UI", "LauncherY", -1f,
                "Saved launcher Y position. -1 vertically centers it on first use.");
            _windowX = Config.Bind("UI", "WindowX", -1f, "Saved journal window X position. -1 centers the window on first use.");
            _windowY = Config.Bind("UI", "WindowY", -1f, "Saved journal window Y position. -1 centers the window on first use.");
            _windowWidth = Config.Bind("UI", "WindowWidth", 720f, "Journal window width in pixels.");
            _windowHeight = Config.Bind("UI", "WindowHeight", 560f, "Journal window height in pixels.");

            string dataDirectory = Path.Combine(Paths.ConfigPath, "ErenshorJournal");
            _store = new JournalStore(Path.Combine(dataDirectory, "journal.dat"));
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning)) Logger.LogWarning("Erenshor Journal recovered from unreadable local data. " + warning);

            _window = new JournalWindow();
            _launcher = new JournalLauncher();
            _windowRect = ResolveInitialRect();
            _launcherRect = ResolveInitialLauncherRect();
            Logger.LogInfo("Erenshor Journal " + PluginVersion + " loaded. Use the draggable Journal UI button to open or close it. Journal does not register a global hotkey. Notes remain local and are never logged or networked.");
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
                Logger.LogError("Erenshor Journal update failed: " + ex);
            }
        }

        private void OnGUI()
        {
            try
            {
                if (_open && _window != null && _document != null)
                {
                    _windowRect = ClampRect(_window.Draw(_windowRect, _document, MarkDirty));
                    if (_window.RequestClose) CloseJournal();
                }

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
                Logger.LogError("Erenshor Journal UI failed: " + ex);
                if (_open) CloseJournal();
            }
        }

        private void OnDestroy()
        {
            try { SaveNow(); } catch { }
            try { PersistWindowRect(); } catch { }
            try { PersistLauncherRect(); } catch { }
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null;
            _launcher = null;
            _document = null;
            _store = null;
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
                Logger.LogError("Erenshor Journal could not save local notes: " + ex.GetType().Name + ": " + ex.Message);
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

            bool previous = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                _windowX.Value = rect.x;
                _windowY.Value = rect.y;
                _windowWidth.Value = rect.width;
                _windowHeight.Value = rect.height;
                Config.Save();
            }
            finally
            {
                Config.SaveOnConfigSet = previous;
            }
        }

        private void PersistLauncherRect()
        {
            if (_launcherX == null || _launcherY == null) return;
            Rect rect = ClampLauncherRect(_launcherRect);

            bool previous = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                _launcherX.Value = rect.x;
                _launcherY.Value = rect.y;
                Config.Save();
                _launcherDirty = false;
            }
            finally
            {
                Config.SaveOnConfigSet = previous;
            }
        }

        private static bool RectsNearlyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.25f &&
                   Mathf.Abs(a.y - b.y) < 0.25f &&
                   Mathf.Abs(a.width - b.width) < 0.25f &&
                   Mathf.Abs(a.height - b.height) < 0.25f;
        }
    }
}
