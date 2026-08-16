using System;
using ErenshorJournal;

// Pure, deterministic tests for character-key composition (JournalCharacterKey.cs). No Unity or
// game assembly dependency - see JournalCharacterIdentity.cs for how the live "name"/"slot" inputs
// are actually resolved from GameData, which is not exercised here.
internal static class JournalCharacterKeyTests
{
    private static int _passed;

    private static void Main()
    {
        Run("slot-qualified key uses slot prefix", TestSlotQualifiedKey);
        Run("no slot falls back to name-only key", TestNameOnlyKeyWhenNoSlot);
        Run("safe key lowercases and strips punctuation", TestSafeKeySanitizes);
        Run("safe key is bounded to 48 characters", TestSafeKeyBounded);
        Run("safe key falls back to 'player' for empty input", TestSafeKeyFallback);
        Run("same name different slots produce different keys", TestDifferentSlotsDifferentKeys);
        Run("same slot reused by a different character name stays isolated", TestSameSlotDifferentName);
        Run("same name and slot are stable across calls", TestStableAcrossCalls);
        Console.WriteLine("PASS: " + _passed + " tests");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void TestSlotQualifiedKey()
    {
        string key = JournalCharacterKey.Compose("Aeliana", 2);
        Assert(key == "slot2_aeliana", "expected slot-qualified, lowercased key; got " + key);
    }

    private static void TestNameOnlyKeyWhenNoSlot()
    {
        string key = JournalCharacterKey.Compose("Aeliana", -1);
        Assert(key == "aeliana", "expected name-only key when slot is unresolved; got " + key);
    }

    private static void TestSafeKeySanitizes()
    {
        string key = JournalCharacterKey.SafeKey("Sir Reginald III!");
        Assert(key == "sir_reginald_iii_", "expected non-alphanumeric characters replaced with '_'; got " + key);
    }

    private static void TestSafeKeyBounded()
    {
        string longName = new string('x', 80);
        string key = JournalCharacterKey.SafeKey(longName);
        Assert(key.Length == 48, "expected key bounded to 48 characters; got length " + key.Length);
    }

    private static void TestSafeKeyFallback()
    {
        Assert(JournalCharacterKey.SafeKey("") == "player", "expected fallback for empty name");
        Assert(JournalCharacterKey.SafeKey(null) == "player", "expected fallback for null name");
    }

    private static void TestDifferentSlotsDifferentKeys()
    {
        string a = JournalCharacterKey.Compose("Bram", 0);
        string b = JournalCharacterKey.Compose("Bram", 1);
        Assert(a != b, "same name in different slots must produce different keys");
    }

    private static void TestSameSlotDifferentName()
    {
        string oldCharacter = JournalCharacterKey.Compose("Bram", 2);
        string newCharacter = JournalCharacterKey.Compose("Tamsin", 2);
        Assert(oldCharacter != newCharacter, "slot reuse with a different character name must not reuse the same journal key");
    }

    private static void TestStableAcrossCalls()
    {
        string first = JournalCharacterKey.Compose("Tamsin", 3);
        string second = JournalCharacterKey.Compose("Tamsin", 3);
        Assert(first == second, "identical inputs must produce an identical key every time");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
