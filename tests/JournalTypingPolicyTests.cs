using System;
using ErenshorJournal;

internal static class JournalTypingPolicyTests
{
    private static int _passed;

    private static void Main()
    {
        Run("gains focus asserts true", TestGainsFocusAssertsTrue);
        Run("already forced true stays idle while still focused", TestStaysIdleWhileFocused);
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

    private static void TestGainsFocusAssertsTrue()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: true, currentlyForced: false, nativeOwnerActive: false);
        Assert(decision.WriteTrue, "expected a write-true when Journal newly gains focus");
        Assert(!decision.WriteFalse, "must not write false while gaining focus");
        Assert(decision.NextForcedState, "Journal should now consider itself the forcing owner");
    }

    private static void TestStaysIdleWhileFocused()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: true, currentlyForced: true, nativeOwnerActive: false);
        Assert(!decision.WriteTrue, "must not repeat the write-true every frame while already forced");
        Assert(!decision.WriteFalse, "must not write false while still focused");
        Assert(decision.NextForcedState, "forced state must persist while still focused");
    }

    private static void TestLosesFocusClearsWhenNoOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: false, currentlyForced: true, nativeOwnerActive: false);
        Assert(!decision.WriteTrue, "must not write true while releasing focus");
        Assert(decision.WriteFalse, "expected a write-false: Journal owns the flag and nothing else needs it");
        Assert(!decision.NextForcedState, "Journal must release its forced-owner claim");
    }

    // This is the exact scenario from the reported bug: Journal set PlayerTyping, native chat
    // also opened and depends on it, Journal loses focus first. The write-false must be
    // suppressed so chat's suppression is not clobbered.
    private static void TestLosesFocusPreservesNativeOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: false, currentlyForced: true, nativeOwnerActive: true);
        Assert(!decision.WriteTrue, "must not write true while releasing focus");
        Assert(!decision.WriteFalse, "must NOT clear PlayerTyping while a native owner still needs it true");
        Assert(!decision.NextForcedState, "Journal must still release its own forced-owner claim even though it left the flag alone");
    }

    private static void TestNeverFocusedStaysIdle()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: false, currentlyForced: false, nativeOwnerActive: false);
        Assert(!decision.WriteTrue, "no focus, no forced state: nothing to assert");
        Assert(!decision.WriteFalse, "no focus, no forced state: nothing to release");
        Assert(!decision.NextForcedState, "state must remain unforced");
    }

    // Journal never asserted the flag (another system owns it), so Journal must never touch it
    // regardless of what native ownership looks like.
    private static void TestNotForcedIgnoresNativeOwner()
    {
        JournalTypingDecision decision = JournalTypingPolicy.Evaluate(wantsTyping: false, currentlyForced: false, nativeOwnerActive: true);
        Assert(!decision.WriteTrue, "Journal does not own the flag; must not write true");
        Assert(!decision.WriteFalse, "Journal does not own the flag; must not write false");
        Assert(!decision.NextForcedState, "state must remain unforced");
    }

    // Verifies switching focus back and forth stays correct even across a native-preserved
    // release: focus lost while chat active (flag preserved, Journal releases its claim), then
    // focus regained (Journal reasserts true, which is a safe idempotent write since the flag is
    // already true from chat).
    private static void TestReacquireAfterPreservedRelease()
    {
        JournalTypingDecision released = JournalTypingPolicy.Evaluate(wantsTyping: false, currentlyForced: true, nativeOwnerActive: true);
        Assert(!released.WriteFalse, "first release must preserve the native owner's flag");
        Assert(!released.NextForcedState, "Journal releases its own claim");

        JournalTypingDecision reacquired = JournalTypingPolicy.Evaluate(wantsTyping: true, currentlyForced: released.NextForcedState, nativeOwnerActive: true);
        Assert(reacquired.WriteTrue, "regaining focus must reassert true, even if already true");
        Assert(reacquired.NextForcedState, "Journal becomes the forcing owner again");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
