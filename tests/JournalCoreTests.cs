using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ErenshorJournal;

internal static class JournalCoreTests
{
    private static int _passed;

    private static void Main()
    {
        Run("default document", TestDefaultDocument);
        Run("tab CRUD keeps one tab", TestTabs);
        Run("tab names and ids normalize safely", TestTabNormalization);
        Run("unicode multiline persistence", TestRoundTrip);
        Run("large note persistence", TestLargeNoteRoundTrip);
        Run("chronicle bounded", TestChronicleBounded);
        Run("chronicle duplicate suppression", TestChronicleDuplicateSuppression);
        Run("structured Chronicle never mutates manual notes", TestStructuredChronicleDoesNotMutateManualNotes);
        Run("stable Chronicle event is exactly once", TestStableChronicleEventExactlyOnce);
        Run("stable Chronicle event remains exactly once after reload", TestStableChronicleEventExactlyOnceAfterReload);
        Run("legacy v1 Chronicle rows still load", TestLegacyV1ChronicleLoads);
        Run("Chronicle persistence remains character separated", TestChroniclePerCharacterStores);
        Run("chronicle exact duplicates repaired on load", TestChronicleExactDuplicateRepair);
        Run("chronicle payload bounds", TestChroniclePayloadBounds);
        Run("missing file starts clean", TestMissingFile);
        Run("malformed record preserves readable data", TestMalformedRecordPartialRecovery);
        Run("empty file recovers safely", TestEmptyFileRecovery);
        Run("truncated main recovers backup", TestTruncatedMainRecoversBackup);
        Run("complete first-save temp is recoverable", TestFirstSaveTempRecovery);
        Run("newer complete temp beats older readable main", TestNewerTempRecovery);
        Run("newer complete temp beats older backup after corrupt fallback", TestNewestRecoveryCandidateWins);
        Run("successful replace keeps previous backup", TestAtomicBackupReplacement);
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
        doc.Tabs[selected].Text = "Bring resist gear.";
        Assert(doc.Tabs[selected].Name == "Raid Notes", "rename normalization failed");
        Assert(doc.Tabs[selected].Text == "Bring resist gear.", "edit did not stick");
        Assert(JournalCore.DeleteSelectedTab(doc), "delete selected failed");

        while (doc.Tabs.Count > 1) Assert(JournalCore.DeleteSelectedTab(doc), "expected delete while more than one tab remains");
        Assert(!JournalCore.DeleteSelectedTab(doc), "must keep at least one tab");
    }

    private static void TestTabNormalization()
    {
        JournalDocument doc = new JournalDocument();
        doc.Tabs.Clear();
        JournalTab first = new JournalTab(); first.Id = "duplicate"; first.Name = "  Raid\nNotes\t"; first.Text = null;
        JournalTab second = new JournalTab(); second.Id = "duplicate"; second.Name = new string('x', 80); second.Text = "body";
        JournalTab third = new JournalTab(); third.Id = " "; third.Name = " "; third.Text = "third";
        doc.Tabs.Add(first); doc.Tabs.Add(second); doc.Tabs.Add(third);
        JournalCore.Normalize(doc);
        Assert(doc.Tabs.Count == 3, "normalization should preserve readable tabs");
        Assert(doc.Tabs[0].Id == "duplicate", "first valid id should remain stable");
        Assert(doc.Tabs[1].Id != "duplicate" && doc.Tabs[2].Id.Length > 0, "duplicate/blank ids should be repaired");
        Assert(doc.Tabs[0].Name == "Raid Notes", "illegal tab whitespace should become one line");
        Assert(doc.Tabs[1].Name.Length == JournalCore.MaxTabNameLength, "long tab name should be bounded");
        Assert(doc.Tabs[2].Name == "Untitled", "blank tab name should become Untitled");
        Assert(doc.Tabs[0].Text == string.Empty, "null note body should normalize to empty");
    }

    private static void TestRoundTrip()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument doc = JournalCore.CreateDefault();
            doc.SelectedTabIndex = 1;
            doc.Tabs[0].Text = "Line one\nLine two\nUnicode: café 漢字 ✓\tTabbed";
            Assert(JournalCore.AppendChronicle(doc, "Test Mod", "Milestone", "Defeated something important.", DateTime.UtcNow), "chronicle append failed");
            store.Save(doc);

            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning == null, "unexpected load warning");
            Assert(loaded.SelectedTabIndex == 1, "selected tab was not persisted");
            Assert(loaded.Tabs[0].Text == doc.Tabs[0].Text, "tab text changed during round trip");
            Assert(loaded.Chronicle.Count == 1, "chronicle entry missing");
            Assert(loaded.Chronicle[0].Text == "Defeated something important.", "chronicle text changed");
        });
    }

    private static void TestLargeNoteRoundTrip()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            string large = new string('a', 1024 * 1024) + "\n끝";
            JournalDocument doc = JournalCore.CreateDefault();
            doc.Tabs[0].Text = large;
            store.Save(doc);
            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning == null, "large readable note should not warn");
            Assert(loaded.Tabs[0].Text == large, "large note was changed or truncated");
        });
    }

    private static void TestChronicleBounded()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        DateTime start = DateTime.UtcNow.AddHours(-2);
        for (int i = 0; i < JournalCore.MaxChronicleEntries + 25; i++)
            JournalCore.AppendChronicle(doc, "Tests", "Event", "Entry " + i.ToString(), start.AddSeconds(i));
        Assert(doc.Chronicle.Count == JournalCore.MaxChronicleEntries, "chronicle cap not enforced");
        Assert(doc.Chronicle[0].Text == "Entry 25", "oldest entries were not trimmed first");
    }

    private static void TestChronicleDuplicateSuppression()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        DateTime now = DateTime.UtcNow;
        Assert(JournalCore.AppendChronicle(doc, "Contracts", "Complete", "A contract was completed.", now), "first append should succeed");
        Assert(!JournalCore.AppendChronicle(doc, "contracts", "complete", "A contract was completed.", now.AddSeconds(4)), "short-window exact duplicate should be rejected");
        Assert(JournalCore.AppendChronicle(doc, "Contracts", "Complete", "A contract was completed.", now.AddSeconds(20)), "same event outside duplicate window should be allowed");
        Assert(doc.Chronicle.Count == 2, "duplicate suppression produced wrong count");
    }

    private static void TestStructuredChronicleDoesNotMutateManualNotes()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        doc.Tabs[0].Text = "Player-authored note body.";
        string before = doc.Tabs[0].Text;
        DateTime stamp = new DateTime(638908000000000000L, DateTimeKind.Utc);
        Assert(JournalCore.AppendChronicleEvent(doc, "crafting.foraging.level.2", "Crafting Expanded", "Progression",
            "Foraging reached level 2", "Foraging increased from level 1 to level 2.", stamp), "structured Chronicle append failed");
        Assert(doc.Tabs[0].Text == before, "automated Chronicle event mutated a manual note body");
        Assert(doc.Chronicle.Count == 1, "structured event did not create a distinct Chronicle item");
        Assert(doc.Chronicle[0].Title == "Foraging reached level 2", "structured Chronicle title was not preserved");
        Assert(doc.Chronicle[0].EventId == "crafting.foraging.level.2", "structured Chronicle event id was not preserved");
    }

    private static void TestStableChronicleEventExactlyOnce()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        DateTime stamp = new DateTime(638908000000000000L, DateTimeKind.Utc);
        Assert(JournalCore.AppendChronicleEvent(doc, "contract:occurrence-17", "Erenshor Contracts", "Contract",
            "Completed Global Contract: Grand Tour", "Visited 8 zones. Reward: 420 XP.", stamp), "first stable event should append");
        Assert(!JournalCore.AppendChronicleEvent(doc, "contract:occurrence-17", "erenshor contracts", "Contract",
            "Completed Global Contract: Grand Tour", "duplicate callback body can differ safely", stamp.AddMinutes(30)),
            "same source-owned event id should be rejected even outside the legacy duplicate window");
        Assert(doc.Chronicle.Count == 1, "stable event duplicate created another Chronicle row");
    }

    private static void TestStableChronicleEventExactlyOnceAfterReload()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument doc = JournalCore.CreateDefault();
            DateTime stamp = new DateTime(638908000000000000L, DateTimeKind.Utc);
            Assert(JournalCore.AppendChronicleEvent(doc, "crafting.foraging.level.3", "Crafting Expanded", "Progression",
                "Foraging reached level 3", "Foraging increased from level 2 to level 3.", stamp), "initial stable event failed");
            store.Save(doc);

            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning == null, "structured v2 round trip should not warn");
            Assert(loaded.Chronicle.Count == 1, "stable event missing after reload");
            Assert(loaded.Chronicle[0].EventId == "crafting.foraging.level.3", "stable event id did not persist");
            Assert(loaded.Chronicle[0].Title == "Foraging reached level 3", "structured title did not persist");
            Assert(!JournalCore.AppendChronicleEvent(loaded, "crafting.foraging.level.3", "Crafting Expanded", "Progression",
                "Foraging reached level 3", "replayed callback", stamp.AddDays(1)), "replayed event should be rejected after reload");
            Assert(loaded.Chronicle.Count == 1, "replayed stable event duplicated after reload");
        });
    }

    private static void TestLegacyV1ChronicleLoads()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            DateTime stamp = new DateTime(638908000000000000L, DateTimeKind.Utc);
            string tabId = Convert.ToBase64String(Encoding.UTF8.GetBytes("tab1"));
            string tabName = Convert.ToBase64String(Encoding.UTF8.GetBytes("Journal"));
            string tabText = Convert.ToBase64String(Encoding.UTF8.GetBytes("Manual legacy note"));
            string source = Convert.ToBase64String(Encoding.UTF8.GetBytes("Erenshor Contracts"));
            string category = Convert.ToBase64String(Encoding.UTF8.GetBytes("Contract"));
            string text = Convert.ToBase64String(Encoding.UTF8.GetBytes("Completed Local Contract: Road Check"));
            File.WriteAllText(path,
                "ERENSHOR_JOURNAL_V1\n" +
                "SELECTED\t0\n" +
                "TAB\t" + tabId + "\t" + tabName + "\t" + tabText + "\n" +
                "CHRON\t" + stamp.Ticks.ToString() + "\t" + source + "\t" + category + "\t" + text + "\n",
                Encoding.UTF8);

            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning == null, "valid legacy v1 file should load without warning");
            Assert(loaded.Tabs[0].Text == "Manual legacy note", "legacy manual note was changed");
            Assert(loaded.Chronicle.Count == 1, "legacy Chronicle row was lost");
            Assert(loaded.Chronicle[0].EventId == string.Empty, "legacy Chronicle row should not invent a stable event id");
            Assert(loaded.Chronicle[0].Title == "Completed Local Contract: Road Check", "legacy Chronicle title should derive from its first concise body sentence");
            store.Save(loaded);
            string migratedWarning;
            JournalDocument migrated = store.Load(out migratedWarning);
            Assert(migratedWarning == null, "v1-to-v2 save migration should reload cleanly");
            Assert(migrated.Tabs[0].Text == "Manual legacy note", "v1-to-v2 migration changed a manual note");
            Assert(migrated.Chronicle.Count == 1 && migrated.Chronicle[0].Text == "Completed Local Contract: Road Check", "v1-to-v2 migration changed Chronicle history");
            Assert(migrated.Chronicle[0].Title == "Completed Local Contract: Road Check", "v1-to-v2 migration lost derived Chronicle title");
        });
    }

    private static void TestChroniclePerCharacterStores()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErenshorJournalCharacterTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string aPath = System.IO.Path.Combine(root, "slot0", "journal.dat");
            string bPath = System.IO.Path.Combine(root, "slot1", "journal.dat");
            JournalStore aStore = new JournalStore(aPath);
            JournalStore bStore = new JournalStore(bPath);
            JournalDocument a = JournalCore.CreateDefault();
            JournalDocument b = JournalCore.CreateDefault();
            a.Tabs[0].Text = "A private note";
            b.Tabs[0].Text = "B private note";
            Assert(JournalCore.AppendChronicleEvent(a, "crafting.foraging.level.2", "Crafting Expanded", "Progression",
                "Foraging reached level 2", "A milestone.", DateTime.UtcNow), "character A event failed");
            Assert(JournalCore.AppendChronicleEvent(b, "crafting.foraging.level.5", "Crafting Expanded", "Progression",
                "Foraging reached level 5", "B milestone.", DateTime.UtcNow), "character B event failed");
            aStore.Save(a);
            bStore.Save(b);

            string warningA, warningB;
            JournalDocument loadedA = aStore.Load(out warningA);
            JournalDocument loadedB = bStore.Load(out warningB);
            Assert(warningA == null && warningB == null, "separate character stores should load cleanly");
            Assert(loadedA.Tabs[0].Text == "A private note" && loadedB.Tabs[0].Text == "B private note", "manual note character separation failed");
            Assert(loadedA.Chronicle.Count == 1 && loadedA.Chronicle[0].EventId == "crafting.foraging.level.2", "character A Chronicle leaked or changed");
            Assert(loadedB.Chronicle.Count == 1 && loadedB.Chronicle[0].EventId == "crafting.foraging.level.5", "character B Chronicle leaked or changed");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void TestChronicleExactDuplicateRepair()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument doc = JournalCore.CreateDefault();
            DateTime stamp = new DateTime(638900000000000000L, DateTimeKind.Utc);
            JournalChronicleEntry a = new JournalChronicleEntry(); a.TimestampUtc = stamp; a.Source = "Contracts"; a.Category = "Complete"; a.Text = "Same";
            JournalChronicleEntry b = new JournalChronicleEntry(); b.TimestampUtc = stamp; b.Source = "contracts"; b.Category = "complete"; b.Text = "Same";
            doc.Chronicle.Add(a); doc.Chronicle.Add(b);
            store.Save(doc);
            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning != null, "duplicate repair should surface a generic warning");
            Assert(loaded.Chronicle.Count == 1, "exact persisted duplicate should be removed");
        });
    }

    private static void TestChroniclePayloadBounds()
    {
        JournalDocument doc = JournalCore.CreateDefault();
        string source = new string('s', JournalCore.MaxChronicleSourceLength + 20) + "\nprivate";
        string category = new string('c', JournalCore.MaxChronicleCategoryLength + 20);
        string text = new string('x', JournalCore.MaxChronicleTextLength + 200) + "\0tail";
        Assert(JournalCore.AppendChronicle(doc, source, category, text, DateTime.UtcNow), "bounded append failed");
        AssertLength(doc.Chronicle[0].Source, JournalCore.MaxChronicleSourceLength, "source");
        AssertLength(doc.Chronicle[0].Category, JournalCore.MaxChronicleCategoryLength, "category");
        AssertLength(doc.Chronicle[0].Text, JournalCore.MaxChronicleTextLength, "text");
        Assert(doc.Chronicle[0].Source.IndexOf('\n') < 0, "source should not contain newlines");
        Assert(doc.Chronicle[0].Text.IndexOf('\0') < 0, "text should not contain NULs");
        string eventId = new string('e', JournalCore.MaxChronicleEventIdLength + 30) + "\nprivate";
        string title = new string('t', JournalCore.MaxChronicleTitleLength + 30) + "\nprivate";
        Assert(JournalCore.AppendChronicleEvent(doc, eventId, "Structured", "Progression", title, "Structured body", DateTime.UtcNow.AddMinutes(1)), "structured bounded append failed");
        AssertLength(doc.Chronicle[1].EventId, JournalCore.MaxChronicleEventIdLength, "event id");
        AssertLength(doc.Chronicle[1].Title, JournalCore.MaxChronicleTitleLength, "title");
        Assert(doc.Chronicle[1].EventId.IndexOf('\n') < 0 && doc.Chronicle[1].Title.IndexOf('\n') < 0, "structured labels should not contain newlines");
        Assert(!JournalCore.AppendChronicle(doc, "Unknown Source", "Other", "\0\0 \t", DateTime.UtcNow), "empty malformed payload should be rejected");
    }

    private static void TestMissingFile()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            string warning;
            JournalDocument doc = store.Load(out warning);
            Assert(warning == null, "ordinary missing first-run file should not warn");
            Assert(doc.Tabs.Count == 3, "missing file should create defaults");
        });
    }

    private static void TestMalformedRecordPartialRecovery()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            string goodText = Encode("still readable");
            string goodName = Encode("Journal");
            string goodId = Encode("tab1");
            File.WriteAllText(path,
                "ERENSHOR_JOURNAL_V1\n" +
                "TAB\t" + goodId + "\t" + goodName + "\t" + goodText + "\n" +
                "CHRON\tnot-ticks\t%%%\t%%%\t%%%\n", Encoding.UTF8);

            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning != null, "partial recovery should report a warning");
            Assert(loaded.Tabs.Count == 1, "readable tab should be preserved");
            Assert(loaded.Tabs[0].Text == "still readable", "readable tab text was lost");
            Assert(loaded.Chronicle.Count == 0, "malformed chronicle record should be skipped");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 0, "partial record recovery should not quarantine the whole file");
        });
    }

    private static void TestEmptyFileRecovery()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            File.WriteAllText(path, string.Empty);
            string warning;
            JournalDocument doc = store.Load(out warning);
            Assert(warning != null, "empty data file should warn");
            Assert(doc.Tabs.Count > 0, "empty data file should recover defaults");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 1, "empty data file should be preserved");
        });
    }

    private static void TestTruncatedMainRecoversBackup()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument first = JournalCore.CreateDefault(); first.Tabs[0].Text = "backup version"; store.Save(first);
            JournalDocument second = JournalCore.CreateDefault(); second.Tabs[0].Text = "new main"; store.Save(second);
            Assert(File.Exists(path + ".bak"), "second save should leave a backup");
            File.WriteAllText(path, "ERENSHOR_JOURNAL_V1\nSELECTED\t0\n", Encoding.UTF8);
            string warning;
            JournalDocument loaded = store.Load(out warning);
            Assert(warning != null, "truncated main should warn");
            Assert(loaded.Tabs[0].Text == "backup version", "truncated main should recover the previous good backup");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 1, "truncated main should be preserved for recovery");
        });
    }

    private static void TestFirstSaveTempRecovery()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument doc = JournalCore.CreateDefault(); doc.Tabs[0].Text = "temp recovery";
            store.Save(doc);
            File.Move(path, path + ".tmp");
            string warning;
            JournalDocument recovered = store.Load(out warning);
            Assert(warning != null, "temp-only recovery should report recovery state");
            Assert(recovered.Tabs[0].Text == "temp recovery", "complete first-save temp should be recoverable");
        });
    }

    private static void TestNewerTempRecovery()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument old = JournalCore.CreateDefault(); old.Tabs[0].Text = "old main"; store.Save(old);
            // Ensure a distinct, newer filesystem timestamp even on coarse-resolution filesystems.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-5));

            string tempPath = path + ".tmp";
            JournalDocument newer = JournalCore.CreateDefault(); newer.Tabs[0].Text = "newer interrupted save";
            JournalStore tempStore = new JournalStore(tempPath);
            tempStore.Save(newer);
            File.SetLastWriteTimeUtc(tempPath, DateTime.UtcNow);

            string warning;
            JournalDocument recovered = store.Load(out warning);
            Assert(warning != null, "newer temp recovery should identify recovery state");
            Assert(recovered.Tabs[0].Text == "newer interrupted save", "validated newer temp should win over the older readable main");
        });
    }

    private static void TestNewestRecoveryCandidateWins()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument backup = JournalCore.CreateDefault(); backup.Tabs[0].Text = "older backup";
            JournalStore backupStore = new JournalStore(path + ".bak"); backupStore.Save(backup);
            File.SetLastWriteTimeUtc(path + ".bak", DateTime.UtcNow.AddSeconds(-10));

            JournalDocument temp = JournalCore.CreateDefault(); temp.Tabs[0].Text = "newest complete temp";
            JournalStore tempStore = new JournalStore(path + ".tmp"); tempStore.Save(temp);
            File.SetLastWriteTimeUtc(path + ".tmp", DateTime.UtcNow);

            File.WriteAllText(path, "truncated live write", Encoding.UTF8);
            string warning;
            JournalDocument recovered = store.Load(out warning);
            Assert(warning != null, "corrupt-main recovery should identify recovery state");
            Assert(recovered.Tabs[0].Text == "newest complete temp", "newest validated recovery candidate should beat the older backup");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 1, "unreadable live file should still be preserved");
        });
    }

    private static void TestAtomicBackupReplacement()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            JournalDocument first = JournalCore.CreateDefault(); first.Tabs[0].Text = "first"; store.Save(first);
            JournalDocument second = JournalCore.CreateDefault(); second.Tabs[0].Text = "second"; store.Save(second);
            Assert(File.Exists(path) && File.Exists(path + ".bak"), "replacement should retain live and backup files");
            File.Delete(path);
            string warning;
            JournalDocument recovered = store.Load(out warning);
            Assert(warning != null, "backup-only recovery should identify recovery state");
            Assert(recovered.Tabs[0].Text == "first", "backup should contain the previous committed version");
        });
    }

    private static void TestCorruptRecovery()
    {
        WithTempStore(delegate(string root, string path, JournalStore store)
        {
            File.WriteAllText(path, "not a journal");
            string warning;
            JournalDocument doc = store.Load(out warning);
            Assert(warning != null, "corrupt load should produce warning");
            Assert(doc.Tabs.Count > 0, "corrupt load should recover defaults");
            Assert(Directory.GetFiles(root, "journal.dat.corrupt-*").Length == 1, "corrupt copy should be preserved");
        });
    }

    private delegate void StoreTest(string root, string path, JournalStore store);

    private static void WithTempStore(StoreTest test)
    {
        string root = Path.Combine(Path.GetTempPath(), "ErenshorJournalTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "journal.dat");
            test(root, path, new JournalStore(path));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static void AssertLength(string value, int max, string field)
    {
        Assert(value != null && value.Length <= max, field + " exceeds bound");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
