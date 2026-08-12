using System;
using System.Collections.Generic;

namespace ErenshorJournal
{
    /// <summary>
    /// Tiny optional integration surface for other Erenshor mods.
    /// Callers should prefer reflection if they do not want a hard dependency on ErenshorJournal.dll.
    /// Entries are queued and applied by the Journal plugin on the Unity main thread.
    /// </summary>
    public static class JournalApi
    {
        private const int MaximumPendingEntries = 256;
        private static readonly Queue<PendingChronicleEntry> Pending = new Queue<PendingChronicleEntry>();

        public static bool AddChronicleEntry(string source, string category, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            PendingChronicleEntry entry = new PendingChronicleEntry();
            entry.TimestampUtc = DateTime.UtcNow;
            entry.Source = source == null ? string.Empty : source;
            entry.Category = category == null ? string.Empty : category;
            entry.Text = text;

            lock (Pending)
            {
                if (Pending.Count >= MaximumPendingEntries) return false;
                Pending.Enqueue(entry);
            }
            return true;
        }

        internal static bool TryDequeue(out PendingChronicleEntry entry)
        {
            lock (Pending)
            {
                if (Pending.Count == 0)
                {
                    entry = null;
                    return false;
                }
                entry = Pending.Dequeue();
                return true;
            }
        }
    }

    internal sealed class PendingChronicleEntry
    {
        internal DateTime TimestampUtc;
        internal string Source;
        internal string Category;
        internal string Text;
    }
}
