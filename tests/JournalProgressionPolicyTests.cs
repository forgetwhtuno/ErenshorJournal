using System;
using ErenshorJournal;

internal static class JournalProgressionPolicyTests
{
    private static int _passed;

    private static void Main()
    {
        Run("first observation is baseline only", TestBaselineOnly);
        Run("unchanged level creates no Chronicle milestone", TestUnchangedLevelFiltered);
        Run("level increase creates one meaningful milestone", TestLevelIncrease);
        Run("duplicate level callback creates no duplicate", TestDuplicateFiltered);
        Run("multi-level jump creates one concise milestone", TestLevelJump);
        Run("character switch re-baselines safely", TestCharacterSeparation);
        Run("observed rollback re-baselines without history", TestRollbackRebaseline);
        Console.WriteLine("PASS: " + _passed + " progression policy tests");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void TestBaselineOnly()
    {
        JournalProgressionLevelTracker tracker = new JournalProgressionLevelTracker();
        tracker.ResetCharacter("slot0_hero");
        JournalProgressionMilestone milestone;
        Assert(!tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 7, out milestone),
            "loading an already-level-7 character must not manufacture old history");
        Assert(milestone == null, "baseline observation returned a milestone");
    }

    private static void TestUnchangedLevelFiltered()
    {
        JournalProgressionLevelTracker tracker = Baseline(3);
        JournalProgressionMilestone milestone;
        Assert(!tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 3, out milestone),
            "same level should be ignored; raw XP ticks between levels must not become Chronicle rows");
    }

    private static void TestLevelIncrease()
    {
        JournalProgressionLevelTracker tracker = Baseline(1);
        JournalProgressionMilestone milestone;
        Assert(tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 2, out milestone),
            "level increase should create a milestone");
        Assert(milestone != null, "milestone missing");
        Assert(milestone.EventId == "crafting.foraging.level.2", "stable event id is wrong");
        Assert(milestone.Category == "Progression", "progression category is wrong");
        Assert(milestone.Title == "Foraging reached level 2", "milestone title is wrong");
        Assert(milestone.Text == "Foraging increased from level 1 to level 2.", "milestone summary is wrong");
    }

    private static void TestDuplicateFiltered()
    {
        JournalProgressionLevelTracker tracker = Baseline(1);
        JournalProgressionMilestone milestone;
        Assert(tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 2, out milestone), "first increase missing");
        Assert(!tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 2, out milestone),
            "duplicate callback for the same observed level should be filtered before Chronicle admission");
    }

    private static void TestLevelJump()
    {
        JournalProgressionLevelTracker tracker = new JournalProgressionLevelTracker();
        tracker.ResetCharacter("slot0_hero");
        JournalProgressionMilestone milestone;
        tracker.Observe("slot0_hero", "crafting.crafting", "Crafting Expanded", "Crafting", 2, out milestone);
        Assert(tracker.Observe("slot0_hero", "crafting.crafting", "Crafting Expanded", "Crafting", 5, out milestone),
            "multi-level jump should still produce a concise current milestone");
        Assert(milestone.Title == "Crafting reached level 5", "jump title should describe the current level");
        Assert(milestone.Text.IndexOf("previous observed level 2", StringComparison.Ordinal) >= 0, "jump summary should not pretend each intermediate level callback was observed");
    }

    private static void TestCharacterSeparation()
    {
        JournalProgressionLevelTracker tracker = Baseline(4);
        tracker.ResetCharacter("slot1_hero");
        JournalProgressionMilestone milestone;
        Assert(!tracker.Observe("slot1_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 12, out milestone),
            "a new character must baseline its current level rather than inherit the old character tracker");
        Assert(tracker.Observe("slot1_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 13, out milestone),
            "new character should track future level increases after baseline");
        Assert(milestone.EventId == "crafting.foraging.level.13", "new character milestone id is wrong");
    }

    private static void TestRollbackRebaseline()
    {
        JournalProgressionLevelTracker tracker = Baseline(8);
        JournalProgressionMilestone milestone;
        Assert(!tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 2, out milestone),
            "lower observed level should fail closed rather than manufacture a progression event");
        Assert(tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", 3, out milestone),
            "tracker should safely continue from the new baseline after rollback/reload state");
        Assert(milestone.EventId == "crafting.foraging.level.3", "post-rebaseline event id is wrong");
    }

    private static JournalProgressionLevelTracker Baseline(int level)
    {
        JournalProgressionLevelTracker tracker = new JournalProgressionLevelTracker();
        tracker.ResetCharacter("slot0_hero");
        JournalProgressionMilestone ignored;
        tracker.Observe("slot0_hero", "crafting.foraging", "Crafting Expanded", "Foraging", level, out ignored);
        return tracker;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
