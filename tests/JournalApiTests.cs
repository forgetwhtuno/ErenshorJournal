using System;
using ErenshorJournal;

namespace ErenshorJournal
{
    // Minimal plugin-shaped test double used only to exercise the reflection-friendly queue without
    // Lunaris/Unity references. Production JournalApi binds to the real plugin class.
    public sealed class ErenshorJournalPlugin
    {
        internal static ErenshorJournalPlugin Instance;
        internal string ControlCharacterKey { get; set; }
    }
}

internal static class JournalApiTests
{
    private static int _passed;

    private static void Main()
    {
        Run("API unavailable without plugin", TestUnavailable);
        Run("stable pending callback dedupes", TestPendingStableDedupe);
        Run("queued event is character scoped", TestCharacterScope);
        Run("legacy v1 API remains available", TestLegacyApi);
        Console.WriteLine("PASS: " + _passed + " Journal API tests");
    }

    private static void Run(string name, Action test)
    {
        Reset();
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void TestUnavailable()
    {
        Assert(!JournalApi.IsAvailable, "API should be unavailable without a live Journal plugin");
        Assert(!JournalApi.AddChronicleEvent("event-1", "Source", "Progression", "Title", "Body"), "unavailable API accepted an event");
    }

    private static void TestPendingStableDedupe()
    {
        ErenshorJournalPlugin.Instance = new ErenshorJournalPlugin();
        ErenshorJournalPlugin.Instance.ControlCharacterKey = "slot0_hero";
        Assert(JournalApi.AddChronicleEvent("level.2", "Crafting Expanded", "Progression", "Foraging reached level 2", "Body"), "first stable event should queue");
        Assert(!JournalApi.AddChronicleEvent("level.2", "crafting expanded", "Progression", "Changed title", "Changed body"),
            "duplicate stable callback should be rejected while already pending");
        PendingChronicleEntry pending;
        Assert(JournalApi.TryDequeue(out pending) && pending != null, "pending event missing");
        Assert(pending.EventId == "level.2", "event id changed in queue");
        Assert(pending.Title == "Foraging reached level 2", "title changed in queue");
        Assert(!JournalApi.TryDequeue(out pending), "duplicate stable callback created a second queued event");
    }

    private static void TestCharacterScope()
    {
        ErenshorJournalPlugin.Instance = new ErenshorJournalPlugin();
        ErenshorJournalPlugin.Instance.ControlCharacterKey = "slot3_alt";
        Assert(JournalApi.AddChronicleEvent("milestone-1", "Source", "Progression", "Title", "Body"), "character-scoped event failed to queue");
        PendingChronicleEntry pending;
        Assert(JournalApi.TryDequeue(out pending), "character-scoped event missing");
        Assert(pending.CharacterKey == "slot3_alt", "queued event did not capture active character scope");
    }

    private static void TestLegacyApi()
    {
        Assert(JournalApi.ContractVersion == 1, "legacy API contract version changed");
        Assert(JournalApi.EventContractVersion == 2, "structured API contract version is wrong");
        ErenshorJournalPlugin.Instance = new ErenshorJournalPlugin();
        ErenshorJournalPlugin.Instance.ControlCharacterKey = "slot0_hero";
        Assert(JournalApi.AddChronicleEntry("Erenshor Contracts", "Contract", "Completed Local Contract: Road Check"),
            "legacy v1 caller should remain supported");
        PendingChronicleEntry pending;
        Assert(JournalApi.TryDequeue(out pending), "legacy pending entry missing");
        Assert(pending.EventId == string.Empty, "legacy entry should not invent a source-owned event id");
        Assert(pending.Title == "Completed Local Contract: Road Check", "legacy entry should derive a useful title from its body");
    }

    private static void Reset()
    {
        JournalApi.ClearPending();
        ErenshorJournalPlugin.Instance = null;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
