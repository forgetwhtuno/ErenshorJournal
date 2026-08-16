using System;
using System.IO;
using System.Text;

namespace ErenshorJournal
{
    // "First character to load claims the legacy global journal.dat once" policy. Journal used a
    // single shared journal.dat before per-character storage existed, and real player notes already
    // live in that file, so it is NEVER deleted or truncated by this migration. The one-time claim
    // marker is acquired durably BEFORE the copy: if a process dies during migration, later
    // characters fail closed instead of inheriting the same private legacy notes.
    internal static class JournalLegacyMigration
    {
        internal static bool ShouldClaim(bool legacyExists, bool claimMarkerExists, bool characterFileExists)
        {
            return legacyExists && !claimMarkerExists && !characterFileExists;
        }

        internal static void ClaimIfEligible(string legacyPath, string characterPath, string claimMarkerPath)
        {
            bool legacyExists = File.Exists(legacyPath);
            bool claimMarkerExists = File.Exists(claimMarkerPath);
            bool characterFileExists = File.Exists(characterPath);
            if (!ShouldClaim(legacyExists, claimMarkerExists, characterFileExists)) return;

            string markerDirectory = Path.GetDirectoryName(claimMarkerPath);
            if (!string.IsNullOrEmpty(markerDirectory)) Directory.CreateDirectory(markerDirectory);

            // CreateNew is the claim transaction. Never remove this marker if the copy later fails:
            // losing automatic migration is recoverable because the untouched legacy file remains,
            // while allowing another character to claim private notes is not.
            try
            {
                using (FileStream stream = new FileStream(claimMarkerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(DateTime.UtcNow.ToString("O"));
                    writer.Flush();
                    stream.Flush(true);
                }
            }
            catch (IOException)
            {
                // Another claimant (or an earlier interrupted claim) already owns the migration.
                return;
            }

            string characterDirectory = Path.GetDirectoryName(characterPath);
            if (!string.IsNullOrEmpty(characterDirectory)) Directory.CreateDirectory(characterDirectory);
            File.Copy(legacyPath, characterPath, false);
        }
    }
}
