using System;
using System.IO;

namespace ErenshorJournal
{
    // "First character to load claims the legacy global journal.dat, once" policy. Journal used a
    // single shared journal.dat before per-character storage existed, and real player notes already
    // live in that file, so it is NEVER deleted or truncated by this migration - only copied into
    // exactly one character's new per-character file, then marked claimed via a companion marker
    // file next to the legacy file so no later character silently inherits the same notes. Every
    // character that first loads after the legacy data is claimed starts with an empty notebook.
    internal static class JournalLegacyMigration
    {
        // Pure decision, unit-tested in tests/JournalLegacyMigrationTests.cs.
        internal static bool ShouldClaim(bool legacyExists, bool claimMarkerExists, bool characterFileExists)
        {
            return legacyExists && !claimMarkerExists && !characterFileExists;
        }

        // Copies the legacy file's bytes into characterPath (creating its directory) and writes the
        // claim marker, but never deletes, truncates, or renames the legacy source. Safe to call on
        // every first-load of a character store: it is a no-op unless ShouldClaim(...) says yes.
        internal static void ClaimIfEligible(string legacyPath, string characterPath, string claimMarkerPath)
        {
            bool legacyExists = File.Exists(legacyPath);
            bool claimMarkerExists = File.Exists(claimMarkerPath);
            bool characterFileExists = File.Exists(characterPath);
            if (!ShouldClaim(legacyExists, claimMarkerExists, characterFileExists)) return;

            string characterDirectory = Path.GetDirectoryName(characterPath);
            if (!string.IsNullOrEmpty(characterDirectory)) Directory.CreateDirectory(characterDirectory);
            File.Copy(legacyPath, characterPath, false);

            string markerDirectory = Path.GetDirectoryName(claimMarkerPath);
            if (!string.IsNullOrEmpty(markerDirectory)) Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(claimMarkerPath, DateTime.UtcNow.ToString("O"));
        }
    }
}
