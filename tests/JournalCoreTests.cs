using System;
using System.IO;
using ErenshorJournal;

internal static class JournalCoreTests
{
    private static int _passed;

    private static void Main()
    {
        Run("default document", TestDefaultDocument);
        Run("tab add rename delete", TestTabs);
        Run("unicode multiline round trip", TestRoundTrip);
        Run("chronicle bounded", TestChronicleBounded);
        Run("corrupt data recovers", TestCorruptRecovery);
        Console.WriteLine("PASS: " + _passed + " tests");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void TestDefaultDocument()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        Assert(doc.Tabs.Count == 3, "expected three default tabs");
        Assert(doc.Tabs[0].Name == "Journal", "first tab should be Journal");
        Assert(doc.Chronicle.Count == 0, "chronicle should start empty");
    }

    private static void TestTabs()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        Assert(JournalCore.AddTab(doc), "add tab failed");
        int selected = doc.SelectedTabIndex;
        doc.Tabs[selected].Name = JournalCore.CleanTabName("  Raid Notes  ");
        Assert(doc.Tabs[selected].Name == "Raid Notes", "rename normalization failed");
        Assert(JournalCore.DeleteSelectedTab(doc), "delete selected failed");

        while (doc.Tabs.Count > 1) Assert(JournalCore.DeleteSelectedTab(doc), "expected delete while more than one tab remains");
        Assert(!JournalCore.DeleteSelectedTab(doc), "must keep at least one tab");
    }

    private static void TestRoundTrip()
    {
        string root = Path.Combine(Path.GetTempPath(), "ErenshorJournalTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "journal.dat");
            JournalStore store = new JournalStore(path);
            JournalDocument doc = JournalCore.CreateDefault();
            doc.Tabs[0].Text = "Line one\nLine two\nUnicode: café 漢字 ✓\tTabbed";
            JournalCore.AppendChronicle(doc, "Test Mod", "Milestone", "Defeated something important.", DateTime.UtcNow);
            store.Save(doc);

            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning == null, "unexpected load warning");
            Assert(loaded.Tabs[0].Text == doc.Tabs[0].Text, "tab text changed during round trip");
            Assert(loaded.Chronicle.Count == 1, "chronicle entry missing");
            Assert(loaded.Chronicle[0].Text == "Defeated something important.", "chronicle text changed");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void TestChronicleBounded()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        for (int i = 0; i < JournalCore.MaxChronicleEntries + 25; i++)
            JournalCore.AppendChronicle(doc, "Tests", "Event", "Entry " + i.ToString(), DateTime.UtcNow);
        Assert(doc.Chronicle.Count == JournalCore.MaxChronicleEntries, "chronicle cap not enforced");
        Assert(doc.Chronicle[0].Text == "Entry 25", "oldest entries were not trimmed first");
    }

    private static void TestCorruptRecovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "ErenshorJournalTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "journal.dat");
            File.WriteAllText(path, "not a journal");
            JournalStore store = new JournalStore(path);
            string warning;
            JournalDocument doc = store.Load(out warning);
            Assert(warning != null, "corrupt load should produce warning");
            Assert(doc.Tabs.Count > 0, "corrupt load should recover defaults");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 1, "corrupt copy should be preserved");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
