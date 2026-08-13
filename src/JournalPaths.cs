using System.IO;

namespace ErenshorJournal
{
    // Small path-composition helper, kept Unity-free so it stays trivially testable. All Journal
    // data lives under plugins/config/ErenshorJournal; per-character notebooks live under a
    // Characters/<key> subfolder, alongside the untouched legacy global journal.dat and its claim
    // marker (see JournalLegacyMigration).
    internal static class JournalPaths
    {
        private const string LegacyFileName = "journal.dat";
        private const string ClaimMarkerFileName = "journal.dat.claimed";
        private const string CharactersFolderName = "Characters";

        internal static string RootDirectory(string baseDirectory)
        {
            return Path.Combine(Path.Combine(baseDirectory, "plugins", "config"), "ErenshorJournal");
        }

        internal static string LegacyJournalPath(string baseDirectory)
        {
            return Path.Combine(RootDirectory(baseDirectory), LegacyFileName);
        }

        internal static string LegacyClaimMarkerPath(string baseDirectory)
        {
            return Path.Combine(RootDirectory(baseDirectory), ClaimMarkerFileName);
        }

        internal static string CharacterJournalPath(string baseDirectory, string characterKey)
        {
            string charactersRoot = Path.Combine(RootDirectory(baseDirectory), CharactersFolderName);
            return Path.Combine(Path.Combine(charactersRoot, characterKey), LegacyFileName);
        }
    }
}
