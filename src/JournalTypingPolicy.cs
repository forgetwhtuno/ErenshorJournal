namespace ErenshorJournal
{
    // Pure decision logic for whether Journal should write GameData.PlayerTyping this frame.
    // Deliberately has no UnityEngine dependency so it can be exercised by a deterministic test
    // without the game or Lunaris assemblies. See NativeTypingOwnership.cs for how
    // "nativeOwnerActive" is actually observed in-game, and ErenshorJournalPlugin.UpdatePlayerTyping
    // for how the resulting decision is applied.
    internal struct JournalTypingDecision
    {
        internal readonly bool WriteTrue;
        internal readonly bool WriteFalse;
        internal readonly bool NextForcedState;

        internal JournalTypingDecision(bool writeTrue, bool writeFalse, bool nextForcedState)
        {
            WriteTrue = writeTrue;
            WriteFalse = writeFalse;
            NextForcedState = nextForcedState;
        }
    }

    internal static class JournalTypingPolicy
    {
        // GameData.PlayerTyping is a bare shared static bool with no ownership encoded in it -
        // at least seven independent native systems (chat, Bank, Auction House, Guild Manager,
        // Raid save window, window-tab rename, GameManager's close-all) each set it directly with
        // no awareness of one another. This policy only ever acts on Journal's OWN transitions,
        // and refuses to write false while a verified native owner is still active, so Journal can
        // never clear a typing suppression another system still needs.
        //
        // wantsTyping: true while Journal's own text field currently has focus.
        // currentlyForced: true if Journal itself is the one that last forced PlayerTyping true.
        // nativeOwnerActive: true if a verified native typing owner (see NativeTypingOwnership) is
        //   currently active - Journal must not clear PlayerTyping in that case.
        internal static JournalTypingDecision Evaluate(bool wantsTyping, bool currentlyForced, bool nativeOwnerActive)
        {
            if (wantsTyping && !currentlyForced)
                return new JournalTypingDecision(true, false, true);

            if (!wantsTyping && currentlyForced)
                return new JournalTypingDecision(false, !nativeOwnerActive, false);

            return new JournalTypingDecision(false, false, currentlyForced);
        }
    }
}
