using System;
using ErenshorJournal;

// Deliberately compiled WITHOUT Crafting Expanded. This proves the Journal-only reflection bridge
// remains failure-closed when the optional sibling is not installed.
internal static class OptionalProgressionBridgeTests
{
    private static void Main()
    {
        OptionalProgressionBridge bridge = new OptionalProgressionBridge();
        bridge.ResetCharacter("slot0_hero", 0f);
        int emitted = 0;
        bool result = bridge.Tick("slot0_hero", 10f, delegate(JournalProgressionMilestone milestone) { emitted++; });
        Assert(!result, "missing optional sibling should not report progression");
        Assert(emitted == 0, "missing optional sibling emitted a Chronicle milestone");
        bridge.ResetForUnload();
        Console.WriteLine("PASS: optional progression bridge fails closed without sibling API");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
