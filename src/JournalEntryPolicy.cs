using System;
using System.Globalization;

namespace ErenshorJournal
{
    // Pure MANUAL note-entry formatting kept outside Unity so the player-authored fast-entry
    // behavior can be regression tested without the game. Automated integrations must never call
    // this helper; they belong in JournalDocument.Chronicle through JournalApi/JournalCore.
    internal static class JournalEntryPolicy
    {
        internal static string AppendTimestampMarker(string current, DateTime localTime, string newLine)
        {
            if (current == null) current = string.Empty;
            if (string.IsNullOrEmpty(newLine)) newLine = Environment.NewLine;

            string separator = string.Empty;
            if (current.Length > 0)
            {
                if (current.EndsWith(newLine + newLine, StringComparison.Ordinal) ||
                    current.EndsWith("\n\n", StringComparison.Ordinal))
                    separator = string.Empty;
                else if (current.EndsWith(newLine, StringComparison.Ordinal) ||
                         current.EndsWith("\n", StringComparison.Ordinal))
                    separator = newLine;
                else
                    separator = newLine + newLine;
            }

            return current + separator + "[" + localTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "] ";
        }
    }
}
