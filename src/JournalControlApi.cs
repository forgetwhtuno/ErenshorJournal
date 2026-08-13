using System;
using System.Collections.Generic;

namespace ErenshorJournal
{
    public sealed class JournalControlState
    {
        public bool GameplayReady;
        public string CharacterKey;
        public bool PanelOpen;
        public int TabCount;
        public int ChronicleCount;
        public string SelectedTabName;
        public int SelectedNoteCharacters;
    }

    public static class JournalControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "journal";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return ErenshorJournalPlugin.Instance != null && ErenshorJournalPlugin.Instance.ControlPanelOpen; } }
        public static JournalControlState GetBasicState()
        {
            JournalControlState state = new JournalControlState();
            state.GameplayReady = SuiteUiPolicy.IsGameplayReady();
            ErenshorJournalPlugin plugin = ErenshorJournalPlugin.Instance;
            if (plugin == null) return state;
            state.CharacterKey = plugin.ControlCharacterKey;
            state.PanelOpen = plugin.ControlPanelOpen;
            JournalDocument doc = plugin.ControlDocument;
            if (doc == null) return state;
            state.TabCount = doc.Tabs == null ? 0 : doc.Tabs.Count;
            state.ChronicleCount = doc.Chronicle == null ? 0 : doc.Chronicle.Count;
            if (doc.Tabs != null && doc.SelectedTabIndex >= 0 && doc.SelectedTabIndex < doc.Tabs.Count)
            {
                JournalTab tab = doc.Tabs[doc.SelectedTabIndex];
                if (tab != null) { state.SelectedTabName = tab.Name ?? string.Empty; state.SelectedNoteCharacters = tab.Text == null ? 0 : tab.Text.Length; }
            }
            return state;
        }
        public static string GetStatus()
        {
            JournalControlState s = GetBasicState();
            return s.GameplayReady ? s.TabCount + " tab(s), " + s.ChronicleCount + " Chronicle entr" + (s.ChronicleCount == 1 ? "y" : "ies") + "." : "Not fully in world.";
        }
        public static bool OpenPanel() { var p = ErenshorJournalPlugin.Instance; if (p == null || !SuiteUiPolicy.IsGameplayReady()) return false; p.RequestOpenJournal(); return true; }
        public static bool ClosePanel() { var p = ErenshorJournalPlugin.Instance; if (p == null) return false; p.RequestCloseJournal(); return true; }
        public static bool GetShowLauncher() { var p = ErenshorJournalPlugin.Instance; return p != null && p.ControlShowStandaloneLauncher; }
        public static bool SetShowLauncher(bool visible) { var p = ErenshorJournalPlugin.Instance; if (p == null) return false; p.SetShowStandaloneLauncher(visible); return true; }
        public static bool ResetPanelPosition() { var p = ErenshorJournalPlugin.Instance; if (p == null) return false; p.ResetWindowPosition(); return true; }
        public static bool ResetLauncherPosition() { var p = ErenshorJournalPlugin.Instance; if (p == null) return false; p.ResetLauncherPosition(); return true; }
    }
}
