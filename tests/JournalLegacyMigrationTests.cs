using System;
using System.IO;
using ErenshorJournal;

// Deterministic tests for the "first character to load claims the legacy global journal.dat once"
// policy (JournalLegacyMigration.cs). No Unity or game assembly dependency.
internal static class JournalLegacyMigrationTests
{
    private static int _passed;

    private static void Main()
    {
        Run("claims when legacy exists, unclaimed, character file absent", TestShouldClaimPositiveCase);
        Run("does not claim when legacy file is absent", TestShouldNotClaimNoLegacy);
        Run("does not claim when already claimed", TestShouldNotClaimAlreadyClaimed);
        Run("does not claim when the character already has its own file", TestShouldNotClaimCharacterFileExists);
        Run("first character copies legacy content and writes the marker", TestClaimCopiesContentAndWritesMarker);
        Run("legacy source file is never deleted or truncated by a claim", TestClaimNeverTouchesLegacySource);
        Run("second character never inherits legacy data once claimed", TestSecondCharacterStartsEmptyAfterClaim);
        Run("claim is a no-op when already claimed (idempotent)", TestClaimIsNoOpWhenAlreadyClaimed);
        Console.WriteLine("PASS: " + _passed + " tests");
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void TestShouldClaimPositiveCase()
    {
        Assert(JournalLegacyMigration.ShouldClaim(legacyExists: true, claimMarkerExists: false, characterFileExists: false),
            "expected a claim to be eligible when legacy data exists, is unclaimed, and the character has no file yet");
    }

    private static void TestShouldNotClaimNoLegacy()
    {
        Assert(!JournalLegacyMigration.ShouldClaim(legacyExists: false, claimMarkerExists: false, characterFileExists: false),
            "no legacy file means nothing to migrate");
    }

    private static void TestShouldNotClaimAlreadyClaimed()
    {
        Assert(!JournalLegacyMigration.ShouldClaim(legacyExists: true, claimMarkerExists: true, characterFileExists: false),
            "a claimed legacy file must never be imported a second time");
    }

    private static void TestShouldNotClaimCharacterFileExists()
    {
        Assert(!JournalLegacyMigration.ShouldClaim(legacyExists: true, claimMarkerExists: false, characterFileExists: true),
            "a character that already has its own file must never be overwritten by legacy data");
    }

    private static void TestClaimCopiesContentAndWritesMarker()
    {
        WithTempRoot(delegate(string root)
        {
            string legacyPath = Path.Combine(root, "journal.dat");
            string markerPath = Path.Combine(root, "journal.dat.claimed");
            string characterPath = Path.Combine(root, "Characters", "aeliana", "journal.dat");
            File.WriteAllText(legacyPath, "LEGACY-CONTENT");

            JournalLegacyMigration.ClaimIfEligible(legacyPath, characterPath, markerPath);

            Assert(File.Exists(characterPath), "expected the character file to be created");
            Assert(File.ReadAllText(characterPath) == "LEGACY-CONTENT", "expected legacy content copied verbatim");
            Assert(File.Exists(markerPath), "expected the claim marker to be written");
        });
    }

    private static void TestClaimNeverTouchesLegacySource()
    {
        WithTempRoot(delegate(string root)
        {
            string legacyPath = Path.Combine(root, "journal.dat");
            string markerPath = Path.Combine(root, "journal.dat.claimed");
            string characterPath = Path.Combine(root, "Characters", "aeliana", "journal.dat");
            File.WriteAllText(legacyPath, "ORIGINAL-DATA");

            JournalLegacyMigration.ClaimIfEligible(legacyPath, characterPath, markerPath);

            Assert(File.Exists(legacyPath), "legacy source must never be deleted");
            Assert(File.ReadAllText(legacyPath) == "ORIGINAL-DATA", "legacy source must never be truncated or modified");
        });
    }

    private static void TestSecondCharacterStartsEmptyAfterClaim()
    {
        WithTempRoot(delegate(string root)
        {
            string legacyPath = Path.Combine(root, "journal.dat");
            string markerPath = Path.Combine(root, "journal.dat.claimed");
            string firstCharacterPath = Path.Combine(root, "Characters", "first", "journal.dat");
            string secondCharacterPath = Path.Combine(root, "Characters", "second", "journal.dat");
            File.WriteAllText(legacyPath, "LEGACY-CONTENT");

            JournalLegacyMigration.ClaimIfEligible(legacyPath, firstCharacterPath, markerPath);
            JournalLegacyMigration.ClaimIfEligible(legacyPath, secondCharacterPath, markerPath);

            Assert(File.Exists(firstCharacterPath), "first character should have claimed the legacy data");
            Assert(!File.Exists(secondCharacterPath), "second character must start fresh, never inheriting the same legacy notes");
        });
    }

    private static void TestClaimIsNoOpWhenAlreadyClaimed()
    {
        WithTempRoot(delegate(string root)
        {
            string legacyPath = Path.Combine(root, "journal.dat");
            string markerPath = Path.Combine(root, "journal.dat.claimed");
            string characterPath = Path.Combine(root, "Characters", "aeliana", "journal.dat");
            File.WriteAllText(legacyPath, "LEGACY-CONTENT");

            JournalLegacyMigration.ClaimIfEligible(legacyPath, characterPath, markerPath);
            File.WriteAllText(characterPath, "EDITED-BY-PLAYER");
            JournalLegacyMigration.ClaimIfEligible(legacyPath, characterPath, markerPath);

            Assert(File.ReadAllText(characterPath) == "EDITED-BY-PLAYER", "a second claim attempt must never overwrite the character's own data");
        });
    }

    private static void WithTempRoot(Action<string> body)
    {
        string root = Path.Combine(Path.GetTempPath(), "ErenshorJournalLegacyMigrationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { body(root); }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
