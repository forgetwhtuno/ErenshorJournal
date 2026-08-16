using System;
using System.Text;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorJournal
{
    // Optional Suite transport only. Note contents, tab names and character keys are never put on
    // the wire; Hub receives a bounded status/count summary plus explicit safe controls.
    internal sealed class JournalSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite.journal.v1.";
        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string> _uiState;
        private IAuraProvider<string, string, string> _settingSet;
        private IAuraProvider<string, string, string> _action;

        internal bool Registered { get; private set; }

        internal JournalSuiteAuraProvider(LunarisPlugin owner)
        {
            if (owner == null) return;
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
            _describe.RegisterFunc(Describe);
            _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
            _basicSettings.RegisterFunc(BasicSettings);
            _uiState = owner.IPCAuraProvider<string>(Prefix + "ui.state");
            _uiState.RegisterFunc(UiState);
            _settingSet = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
            _settingSet.RegisterFunc(SetSetting);
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
            _action.RegisterFunc(InvokeAction);
            Registered = true;
        }

        internal void Unregister()
        {
            SafeUnregister(_describe); _describe = null;
            SafeUnregister(_basicSettings); _basicSettings = null;
            SafeUnregister(_uiState); _uiState = null;
            SafeUnregister(_settingSet); _settingSet = null;
            SafeUnregister(_action); _action = null;
            Registered = false;
        }

        private static void SafeUnregister(IAuraProvider provider)
        {
            if (provider == null) return;
            try { provider.UnregisterFunc(); } catch { }
        }

        private string Describe()
        {
            return "protocol=1"
                + "&module=" + JournalControlApi.ModuleId
                + "&display=" + Uri.EscapeDataString("Journal")
                + "&version=" + Uri.EscapeDataString(ErenshorJournalPlugin.PluginVersion)
                + "&summary=" + Uri.EscapeDataString("Player-owned local notebook and Chronicle view.")
                + "&status=" + Uri.EscapeDataString(SuiteUiControlPolicy.BoundStatus(JournalControlApi.GetStatus()))
                + "&actions=openPanel,closePanel,resetPanel,resetLauncher";
        }

        private string UiState()
        {
            ErenshorJournalPlugin p = ErenshorJournalPlugin.Instance;
            return SuiteUiStatePolicy.Build(JournalControlApi.ModuleId,
                p != null && p.ControlPanelOpen,
                JournalWindow.CanvasSortOrder,
                p == null ? 0d : p.ControlPanelActivatedAt);
        }

        private string BasicSettings()
        {
            StringBuilder sb = new StringBuilder();
            AppendBool(sb, "showLauncher", "Show Journal Launcher", JournalControlApi.GetShowLauncher());
            return sb.ToString();
        }

        private string SetSetting(string settingId, string value)
        {
            if (!string.Equals(settingId, "showLauncher", StringComparison.Ordinal)) return "unknown setting";
            bool parsed;
            if (!SuiteUiControlPolicy.TryParseBool(value, out parsed)) return "rejected";
            return JournalControlApi.SetShowLauncher(parsed) ? "ok" : "rejected";
        }

        private string InvokeAction(string actionId, string argument)
        {
            switch (SuiteUiControlPolicy.ParsePanelAction(actionId))
            {
                case SuitePanelAction.OpenPanel: return JournalControlApi.OpenPanel() ? "ok" : "rejected";
                case SuitePanelAction.ClosePanel: return JournalControlApi.ClosePanel() ? "ok" : "rejected";
                case SuitePanelAction.ResetPanel: return JournalControlApi.ResetPanelPosition() ? "ok" : "rejected";
                case SuitePanelAction.ResetLauncher: return JournalControlApi.ResetLauncherPosition() ? "ok" : "rejected";
                default: return "unknown action";
            }
        }

        private static void AppendBool(StringBuilder sb, string id, string label, bool value)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("id=").Append(Uri.EscapeDataString(id));
            sb.Append("&label=").Append(Uri.EscapeDataString(label));
            sb.Append("&tier=basic&type=bool&value=").Append(value ? "true" : "false");
            sb.Append("&mutable=true");
        }
    }
}
