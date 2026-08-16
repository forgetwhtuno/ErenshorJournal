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
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Harmony | LunarisPermission.Reflection)]
    public sealed class ErenshorJournalPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.journal";
        internal const string PluginName = "Erenshor Journal";
        internal const string PluginVersion = "0.1.8";
        private const int MaximumChronicleIntegrationsPerFrame = 32;

        internal static ErenshorJournalPlugin Instance;
        private bool _initialized;
        private bool _forcedPlayerTyping;
        private JournalSuiteAuraProvider _auraProvider;
        private OptionalProgressionBridge _progressionBridge;
        private Harmony _harmony;

        private JournalSettings _settings;
        private JournalConfigEntry<float> _launcherX;
        private JournalConfigEntry<float> _launcherY;
        private JournalConfigEntry<bool> _showStandaloneLauncherWithHub;
        private JournalConfigEntry<float> _windowX;
        private JournalConfigEntry<float> _windowY;
        private JournalConfigEntry<float> _windowWidth;
        private JournalConfigEntry<float> _windowHeight;
        private JournalConfigEntry<bool> _diagnosticsLogging;

        private JournalStore _store;
        private JournalDocument _document;
        private JournalWindow _window;
        private JournalLauncher _launcher;
        private bool _open;
        private double _panelActivatedAt;
        private bool _dirty;
        private float _saveAfter;
        private bool _pendingExternalOpen;
        private bool _pendingExternalClose;
        private bool _pendingLauncherToggle;
        private bool _cursorVisibleBeforeOpen;
        private CursorLockMode _cursorLockBeforeOpen;

        // True only once a real, local, playable character exists (see
        // JournalCharacterIdentity.IsLocalCharacterReady). Recomputed every frame; never cached
        // across a scene load. Nothing character-scoped (the launcher, the window, or any file
        // load) may happen while this is false.
        private bool _ready;
        // The currently loaded character's stable identity key, or null before any character has
        // ever loaded this session. See JournalCharacterIdentity.ResolveCharacterKey.
        private string _characterKey;

        private bool Diagnostics
        {
            get { return _diagnosticsLogging != null && _diagnosticsLogging.Value; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                try { Logging.LogWarning("Erenshor Journal duplicate plugin instance ignored."); } catch { }
                enabled = false;
                return;
            }
            Instance = this;
            _initialized = true;
            _settings = new JournalSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();
            SuiteUiPolicy.InitializeHubPresence(this);

            // Deliberately does NOT load any journal data here. Journal data is per-character (see
            // EnsureCharacterContext) and must not be touched until a real local character exists;
            // Awake runs at plugin load, well before that is true (e.g. still at the title screen
            // or character-select).
            _window = new JournalWindow();
            _launcher = new JournalLauncher();
            _progressionBridge = new OptionalProgressionBridge();
            InitializeRetainedUi();

            _harmony = new Harmony(PluginGuid);
            try { _harmony.PatchAll(); }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Journal camera compatibility patch failed (" + ex.GetType().Name + ").");
            }

            try { _auraProvider = new JournalSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogWarning("[Journal] Suite Aura provider failed to register (" + ex.GetType().Name + ")."); }

            Logging.LogInfo("Erenshor Journal " + PluginVersion + " loaded. The standalone launcher follows Suite fallback policy once a character is loaded. Journal does not register a global hotkey. Notes remain local, per character, and are never logged or networked.");
        }

        private void InitializeConfigEntries()
        {
            _launcherX = new JournalConfigEntry<float>(delegate { return _settings.LauncherX; }, delegate(float v) { _settings.LauncherX = v; });
            _launcherY = new JournalConfigEntry<float>(delegate { return _settings.LauncherY; }, delegate(float v) { _settings.LauncherY = v; });
            _showStandaloneLauncherWithHub = new JournalConfigEntry<bool>(delegate { return _settings.ShowStandaloneLauncherWithHub; }, delegate(bool v) { _settings.ShowStandaloneLauncherWithHub = v; });
            _windowX = new JournalConfigEntry<float>(delegate { return _settings.WindowX; }, delegate(float v) { _settings.WindowX = v; });
            _windowY = new JournalConfigEntry<float>(delegate { return _settings.WindowY; }, delegate(float v) { _settings.WindowY = v; });
            _windowWidth = new JournalConfigEntry<float>(delegate { return _settings.WindowWidth; }, delegate(float v) { _settings.WindowWidth = v; });
            _windowHeight = new JournalConfigEntry<float>(delegate { return _settings.WindowHeight; }, delegate(float v) { _settings.WindowHeight = v; });
            _diagnosticsLogging = new JournalConfigEntry<bool>(delegate { return _settings.DiagnosticsLogging; }, delegate(bool v) { _settings.DiagnosticsLogging = v; });
        }

        // Recomputes the ready signal fresh every call (cheap null/bool checks - see
        // JournalCharacterIdentity). On a READY -> NOT READY transition: force-closes any open
        // panel (saves + restores cursor via CloseJournal), and drops the character context so nothing
        // character-scoped lingers. Logged unconditionally (not gated behind Diagnostics) since this
        // only fires on login/logout/char-select, not every frame.
        private bool RefreshReadyState()
        {
            bool ready = JournalCharacterIdentity.IsLocalCharacterReady();
            if (ready == _ready) return ready;

            _ready = ready;
            Logging.LogInfo("Erenshor Journal ready-signal is now " + (ready ? "READY" : "NOT READY") + ".");
            if (!ready)
            {
                if (_open) CloseJournal();
                else if (_dirty) SaveNow();
                _characterKey = null;
                _document = null;
                _store = null;
                if (_progressionBridge != null) _progressionBridge.ResetCharacter(string.Empty, Time.unscaledTime);
            }
            return ready;
        }

        // Loads (or migrates-then-loads) the resolved character's notebook when the resolved
        // character key changes while READY. Cheap to call every frame while ready: it is a single
        // string comparison unless the key actually changed. Sequence on an actual change: (1) save
        // the outgoing character's document if dirty, (2) close the panel if open, (3) release the
        // old store/document, (4) resolve the new key, (5) migrate-then-load, (6) rebuild transient
        // UI state - never letting one character's notes leak into another's window, even for one frame.
        private void EnsureCharacterContext()
        {
            string key = JournalCharacterIdentity.ResolveCharacterKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                if (_dirty) SaveNow();
                if (_open) CloseJournal();
                _characterKey = null;
                _document = null;
                _store = null;
                if (_progressionBridge != null) _progressionBridge.ResetCharacter(string.Empty, Time.unscaledTime);
                if (_window != null) _window.ResetTransientState();
                return;
            }
            if (string.Equals(key, _characterKey, StringComparison.Ordinal)) return;

            if (Diagnostics) Logging.LogInfo("[Journal][diag] character context changed.");

            if (_dirty) SaveNow();
            if (_open) CloseJournal();
            _document = null;
            _store = null;

            _characterKey = key;
            string baseDirectory = AppContext.BaseDirectory;
            string legacyPath = JournalPaths.LegacyJournalPath(baseDirectory);
            string claimMarkerPath = JournalPaths.LegacyClaimMarkerPath(baseDirectory);
            string characterPath = JournalPaths.CharacterJournalPath(baseDirectory, key);

            try { JournalLegacyMigration.ClaimIfEligible(legacyPath, characterPath, claimMarkerPath); }
            catch (Exception ex) { Logging.LogError("Erenshor Journal legacy data migration failed (" + ex.GetType().Name + ")."); }

            _store = new JournalStore(characterPath);
            string warning;
            _document = _store.Load(out warning);
            if (!string.IsNullOrEmpty(warning)) Logging.LogWarning("Erenshor Journal recovered readable local data. " + warning);
            if (_progressionBridge != null) _progressionBridge.ResetCharacter(_characterKey, Time.unscaledTime);

            // Rebuild transient UI state (scroll positions, delete-arm timers, cached styles/textures)
            // so nothing from the previous character's session lingers.
            if (_window != null) _window.ResetTransientState();
        }

        private void Update()
        {
            try
            {
                bool ready = RefreshReadyState();
                if (_pendingExternalClose) { _pendingExternalClose = false; if (_open) CloseJournal(); }
                if (_pendingExternalOpen) { _pendingExternalOpen = false; if (ready) { if (_open) MarkPanelActivated(); else OpenJournal(); } }
                if (_pendingLauncherToggle)
                {
                    _pendingLauncherToggle = false;
                    if (ready) ToggleJournal();
                }

                if (ready)
                {
                    EnsureCharacterContext();
                    if (_document != null)
                    {
                        PendingChronicleEntry pending;
                        bool appended = false;
                        int processed = 0;
                        while (processed < MaximumChronicleIntegrationsPerFrame && JournalApi.TryDequeue(out pending))
                        {
                            processed++;
                            if (pending == null || !string.Equals(pending.CharacterKey, _characterKey, StringComparison.Ordinal)) continue;
                            if (JournalCore.AppendChronicleEvent(_document, pending.EventId, pending.Source, pending.Category,
                                pending.Title, pending.Text, pending.TimestampUtc))
                                appended = true;
                        }
                        if (_progressionBridge != null)
                        {
                            _progressionBridge.Tick(_characterKey, Time.unscaledTime, delegate(JournalProgressionMilestone milestone)
                            {
                                if (milestone == null) return;
                                if (JournalCore.AppendChronicleEvent(_document, milestone.EventId, milestone.Source, milestone.Category,
                                    milestone.Title, milestone.Text, DateTime.UtcNow)) appended = true;
                            });
                        }
                        if (appended) MarkDirty();
                    }
                }
                else
                {
                    SuiteDragHandler.ForceReleaseIfOwned();
                }

                bool bridgeRegistered = _auraProvider != null && _auraProvider.Registered;
                bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                    bridgeRegistered,
                    _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value);

                if (_launcher != null) _launcher.Tick(showLauncher, _open);
                if (_window != null) _window.Tick(ready && _open, _document, MarkDirty);
                UpdatePlayerTyping(ready && _open && _window != null && _window.IsTextInputFocused);

                if (_dirty && Time.unscaledTime >= _saveAfter) SaveNow();
            }
            catch (Exception ex)
            {
                try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
                try { UpdatePlayerTyping(false); } catch { }
                Logging.LogError("Erenshor Journal update failed (" + ex.GetType().Name + ").");
            }
        }

        // Only ever forces GameData.PlayerTyping on a transition Journal owns, and never clears
        // it while a verified native owner (native chat, Bank, Auction House, Guild Manager,
        // Raid save window, window-tab rename) is still active - see JournalTypingPolicy.cs and
        // NativeTypingOwnership.cs for the evidence and the actual decision logic. Reads the real
        // current flag value every time: a later native writer can overwrite PlayerTyping to
        // false while Journal's text field is still focused, and _forcedPlayerTyping alone cannot
        // detect that - only the real value can.
        private void UpdatePlayerTyping(bool wantsTyping)
        {
            bool nativeOwnerActive = NativeTypingOwnership.IsAnyNativeOwnerActive();
            JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping, _forcedPlayerTyping, nativeOwnerActive, GameData.PlayerTyping);
            if (decision.WriteTrue) GameData.PlayerTyping = true;
            if (decision.WriteFalse) GameData.PlayerTyping = false;
            _forcedPlayerTyping = decision.NextForcedState;
        }


        private void OnDestroy()
        {
            if (!_initialized) return;
            _initialized = false;
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { SaveNow(); } catch { }
            try { UpdatePlayerTyping(false); } catch { }
            try { NativeTypingOwnership.Reset(); } catch { }
            try { JournalApi.ClearPending(); } catch { }
            try { if (_progressionBridge != null) _progressionBridge.ResetForUnload(); } catch { }
            try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null;
            try { if (_window != null) _window.Dispose(); } catch { }
            try { if (_launcher != null) _launcher.Dispose(); } catch { }
            try { if (_open) RestoreCursor(); } catch { }
            _window = null;
            _launcher = null;
            _progressionBridge = null;
            _document = null;
            _store = null;
            _pendingLauncherToggle = false;
            _pendingExternalOpen = false;
            _pendingExternalClose = false;
            SuiteUiPolicy.Reset();
            if (Instance == this) Instance = null;
        }

        internal bool ControlPanelOpen { get { return _open; } }
        internal double ControlPanelActivatedAt { get { return _panelActivatedAt; } }
        internal string ControlCharacterKey { get { return _characterKey ?? string.Empty; } }
        internal JournalDocument ControlDocument { get { return _document; } }
        internal bool ControlShowStandaloneLauncher { get { return _showStandaloneLauncherWithHub != null && _showStandaloneLauncherWithHub.Value; } }
        internal void SetShowStandaloneLauncher(bool value)
        {
            if (_showStandaloneLauncherWithHub != null) _showStandaloneLauncherWithHub.Value = value;
            try { Config.Save(); } catch { }
        }
        internal void RequestOpenJournal() { _pendingExternalOpen = true; }
        internal void RequestCloseJournal() { _pendingExternalClose = true; }
        internal void ResetLauncherPosition()
        {
            if (_launcher != null) _launcher.ResetPosition();
        }

        private void ToggleJournal()
        {
            if (_open) CloseJournal();
            else OpenJournal();
        }

        private void OpenJournal()
        {
            if (_open) { MarkPanelActivated(); return; }
            _open = true;
            MarkPanelActivated();
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void CloseJournal()
        {
            if (!_open) return;
            SuiteDragHandler.ForceReleaseIfOwned();
            _open = false;
            try { UpdatePlayerTyping(false); } catch { }
            SaveNow();
            RestoreCursor();
        }

        private void MarkPanelActivated()
        {
            _panelActivatedAt = Time.realtimeSinceStartup;
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
                Logging.LogError("Erenshor Journal could not save local notes (" + ex.GetType().Name + ").");
            }
        }

        private void InitializeRetainedUi()
        {
            _window.Initialize(
                _windowX.Value, _windowY.Value, _windowWidth.Value, _windowHeight.Value,
                PersistWindowPosition, PersistWindowSize, RequestCloseJournal, ResetWindowPosition);
            _launcher.Initialize(
                _launcherX.Value, _launcherY.Value,
                PersistLauncherPosition,
                delegate { _pendingLauncherToggle = true; });
        }

        private void PersistWindowPosition(float x, float y)
        {
            if (_windowX == null || _windowY == null) return;
            _windowX.Value = x;
            _windowY.Value = y;
            try { Config.Save(); } catch { }
        }

        private void PersistWindowSize(float width, float height)
        {
            if (_windowWidth == null || _windowHeight == null) return;
            if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height)) return;
            _windowWidth.Value = Mathf.Max(JournalWindow.MinimumWidth, width);
            _windowHeight.Value = Mathf.Max(JournalWindow.MinimumHeight, height);
            try { Config.Save(); } catch { }
        }

        private void PersistLauncherPosition(float x, float y)
        {
            if (_launcherX == null || _launcherY == null) return;
            _launcherX.Value = x;
            _launcherY.Value = y;
            try { Config.Save(); } catch { }
        }

        internal void ResetWindowPosition()
        {
            if (_window != null) _window.ResetPosition();
        }
    }
}
