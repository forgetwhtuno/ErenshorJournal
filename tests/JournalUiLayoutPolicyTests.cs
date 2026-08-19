using System;
using ErenshorJournal;

internal static class JournalUiLayoutPolicyTests
{
    private static int Main()
    {
        Assert(JournalUiLayoutPolicy.MainToolbarDoesNotOverlap(), "Journal toolbar slots overlap");
        Assert(JournalUiLayoutPolicy.TabWidthForName("Journal") == 92f, "short tab should keep compact width");
        Assert(JournalUiLayoutPolicy.TabWidthForName(new string('x', 40)) == 168f, "long tab should cap its width");
        Assert(JournalUiLayoutPolicy.ResolvePanelExtent(760f, 1920f, 440f, 10f) == 760f, "normal screen should preserve preferred width");
        Assert(JournalUiLayoutPolicy.ResolvePanelExtent(760f, 400f, 440f, 10f) == 380f, "small screen should temporarily fit within margins");
        Assert(JournalUiLayoutPolicy.ResolvePanelExtent(320f, 1920f, 440f, 10f) == 440f, "normal screen should honor minimum width");

        // Regression coverage for the 0.1.9 launcher-grip drag defect: a fixed-anchor 20x32 grip
        // sitting at the launcher's bottom-left corner (the production JournalLauncher geometry)
        // must land entirely inside the 154x32 launcher, with no negative or absurd height.
        Assert(JournalUiLayoutPolicy.RectFitsWithinParent(154f, 32f, 0f, 0f, 20f, 32f, 0f, 0f), "fixed-anchor drag grip must fit entirely inside the launcher");
        Assert(!JournalUiLayoutPolicy.RectFitsWithinParent(154f, 32f, 0f, 0f, 20f, 64f, 0f, 0f), "an oversized grip taller than the launcher must be detected as not fitting");
        Assert(!JournalUiLayoutPolicy.RectFitsWithinParent(154f, 32f, 0f, 0f, 20f, -1f, 0f, 0f), "a negative grip height must be rejected");
        Assert(!JournalUiLayoutPolicy.RectFitsWithinParent(154f, 32f, 0f, 8f, 20f, 32f, 0f, 0f), "a grip pushed above the launcher's top edge must be detected as not fitting");
        Console.WriteLine("PASS Journal UI layout policy");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
