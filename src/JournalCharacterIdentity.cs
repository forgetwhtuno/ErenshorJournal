using System;

namespace ErenshorJournal
{
    // Live game-state wiring around the pure JournalCharacterKey composition. The ready-signal and
    // character-key resolution here mirror Erenshor-Nemesis's NemesisDirector (Ready(),
    // ResolveCharacterKey(), ResolveSlotIndex()), which is already live-tested with real gameplay.
    // Deliberately not scene-name string matching: PlayerControl.Myself is the verified signal.
    internal static class JournalCharacterIdentity
    {
        // Cheap null/bool checks only - safe to call every frame, and must never be cached across a
        // scene load (the player object is destroyed/recreated on zone and character transitions).
        internal static bool IsLocalCharacterReady()
        {
            return SuiteUiPolicy.IsGameplayReady();
        }

        internal static string ResolveCharacterKey()
        {
            return JournalCharacterKey.Compose(PlayerName(), ResolveSlotIndex());
        }

        internal static string PlayerName()
        {
            try
            {
                string name = GameData.PlayerControl.Myself.MyStats.MyName;
                return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            }
            catch { return "Player"; }
        }

        // Two save slots can hold the same character name, so persistence keys from the verified
        // slot index when the slot's recorded name matches the live character, and from the name
        // alone otherwise - same reasoning as NemesisDirector.ResolveSlotIndex.
        private static int ResolveSlotIndex()
        {
            try
            {
                SaveGameData active = GameData.CurrentCharacterSlot != null ? GameData.CurrentCharacterSlot : GameData.ActiveSaveSlot;
                if (active == null || active.index < 0) return -1;
                string recorded = (active.CharName ?? "").Trim();
                if (recorded.Length > 0 && !string.Equals(recorded, PlayerName(), StringComparison.OrdinalIgnoreCase)) return -1;
                return active.index;
            }
            catch { return -1; }
        }
    }
}
