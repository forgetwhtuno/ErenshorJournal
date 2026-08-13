using System;
using Lunaris.Config;

namespace ErenshorJournal
{
    // Loader-neutral ConfigEntry-style shim. Keeping the Value surface makes the Lunaris
    // migration mechanical and lets the existing call sites keep their proven access pattern.
    internal sealed class JournalConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal JournalConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class JournalSettings
    {
        public JournalSettings() { }

        [Config("LauncherX", "UI", "Saved launcher X position. -1 places it near the right side of the screen on first use.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Saved launcher Y position. -1 vertically centers it on first use.")]
        public float LauncherY = -1f;

        [Config("WindowX", "UI", "Saved journal window X position. -1 centers the window on first use.")]
        public float WindowX = -1f;

        [Config("WindowY", "UI", "Saved journal window Y position. -1 centers the window on first use.")]
        public float WindowY = -1f;

        [Config("WindowWidth", "UI", "Journal window width in pixels.")]
        public float WindowWidth = 720f;

        [Config("WindowHeight", "UI", "Journal window height in pixels.")]
        public float WindowHeight = 560f;

        [Config("DiagnosticsLogging", "Debug", "When enabled, logs extra low-noise diagnostic lines for the launcher click / open-close / character-switch chain. Off by default.")]
        public bool DiagnosticsLogging = false;
    }
}
