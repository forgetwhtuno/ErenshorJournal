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
        Console.WriteLine("PASS Journal UI layout policy");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
