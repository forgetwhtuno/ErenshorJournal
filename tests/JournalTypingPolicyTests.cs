using System;
using ErenshorJournal;

internal static class JournalTypingPolicyTests
{
    private static int _passed;

    private static void Main()
    {
        Run("gains focus asserts true", TestGainsFocusAssertsTrue);
        Run("already forced true, flag already true: no redundant write", TestStaysIdleWhileFocusedAndFlagTrue);
        Run("focused and forced but flag was cleared externally: reasserts true", TestReassertsWhenExternallyCleared);
        Run("loses focus with no native owner clears true", TestLosesFocusClearsWhenNoOwner);
        Run("loses focus while native owner active leaves flag untouched", TestLosesFocusPreservesNativeOwner);
        Run("never focused and no native owner stays idle", TestNeverFocusedStaysIdle);
        Run("not forced and native owner active stays idle", TestNotForcedIgnoresNativeOwner);
        Run("re-gain focus after native-preserved release re-asserts true", TestReacquireAfterPreservedRelease);
        Console.WriteLine("PASS: " + _passed + " tests");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    // Case A: wants=true, forced=true, currentPlayerTyping=false => WriteTrue=true.
    private static void TestGainsFocusAssertsTrue()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: true, currentlyForced: true, nativeOwnerActive: false, currentPlayerTyping: false);
        Assert(decision.WriteTrue, "expected a write-true: the shared flag was found false while Journal still wants typing");
        Assert(!decision.WriteFalse, "must not write false while wanting typing");
        Assert(decision.NextForcedState, "Journal remains the forcing owner");
    }

    // Case B: wants=true, forced=true, currentPlayerTyping=true => no redundant write required.
    private static void TestStaysIdleWhileFocusedAndFlagTrue()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: true, currentlyForced: true, nativeOwnerActive: false, currentPlayerTyping: true);
        Assert(!decision.WriteTrue, "must not repeat the write when the flag is already true");
        Assert(!decision.WriteFalse, "must not write false while still focused");
        Assert(decision.NextForcedState, "forced state must persist while still focused");
    }

    // Case F: Journal focused, an external writer clears the flag, next evaluation must reassert.
    // Also covers wants=true starting from currentlyForced=false (first frame Journal notices).
    private static void TestReassertsWhenExternallyCleared()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: true, currentlyForced: true, nativeOwnerActive: true, currentPlayerTyping: false);
        Assert(decision.WriteTrue, "an external writer clearing the flag while Journal still wants typing must be corrected");
        Assert(!decision.WriteFalse, "must not write false while wanting typing");
        Assert(decision.NextForcedState, "Journal remains the forcing owner after reasserting");
    }

    // Case D: wants=false, forced=true, nativeOwner=false, currentPlayerTyping=true => write false.
    private static void TestLosesFocusClearsWhenNoOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: false, currentlyForced: true, nativeOwnerActive: false, currentPlayerTyping: true);
        Assert(!decision.WriteTrue, "must not write true while releasing focus");
        Assert(decision.WriteFalse, "expected a write-false: Journal owns the flag and nothing else needs it");
        Assert(!decision.NextForcedState, "Journal must release its forced-owner claim");
    }

    // Case C: wants=false, forced=true, nativeOwner=true, currentPlayerTyping=true => do not write false.
    // This is the exact scenario from the original reported bug.
    private static void TestLosesFocusPreservesNativeOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: false, currentlyForced: true, nativeOwnerActive: true, currentPlayerTyping: true);
        Assert(!decision.WriteTrue, "must not write true while releasing focus");
        Assert(!decision.WriteFalse, "must NOT clear PlayerTyping while a native owner still needs it true");
        Assert(!decision.NextForcedState, "Journal must still release its own forced-owner claim even though it left the flag alone");
    }

    private static void TestNeverFocusedStaysIdle()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: false, currentlyForced: false, nativeOwnerActive: false, currentPlayerTyping: false);
        Assert(!decision.WriteTrue, "no focus, no forced state: nothing to assert");
        Assert(!decision.WriteFalse, "no focus, no forced state: nothing to release");
        Assert(!decision.NextForcedState, "state must remain unforced");
    }

    // Case E: wants=false, forced=false => do nothing, regardless of native ownership.
    private static void TestNotForcedIgnoresNativeOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(
            wantsTyping: false, currentlyForced: false, nativeOwnerActive: true, currentPlayerTyping: true);
        Assert(!decision.WriteTrue, "Journal does not own the flag; must not write true");
        Assert(!decision.WriteFalse, "Journal does not own the flag; must not write false");
        Assert(!decision.NextForcedState, "state must remain unforced");
    }

    // Verifies switching focus back and forth stays correct even across a native-preserved
    // release: focus lost while chat active (flag preserved, Journal releases its claim), then
    // focus regained (Journal reasserts true because the real flag value is read, not assumed).
    private static void TestReacquireAfterPreservedRelease()
    {
        JournalTypingDecision released = JournalTypingPolicy.Evaluate(
            wantsTyping: false, currentlyForced: true, nativeOwnerActive: true, currentPlayerTyping: true);
        Assert(!released.WriteFalse, "first release must preserve the native owner's flag");
        Assert(!released.NextForcedState, "Journal releases its own claim");

        JournalTypingDecision reacquired = JournalTypingPolicy.Evaluate(
            wantsTyping: true, currentlyForced: released.NextForcedState, nativeOwnerActive: true, currentPlayerTyping: true);
        Assert(!reacquired.WriteTrue, "flag is already true (native owner still holds it); no redundant write needed");
        Assert(reacquired.NextForcedState, "Journal becomes the forcing owner again");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
