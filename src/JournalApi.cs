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
        // Kept at v1 so existing optional callers such as Contracts continue to bind without change.
        public const int ContractVersion = 1;
        // New callers that can provide a stable source-owned event id and title may use the v2 method
        // for durable exactly-once Chronicle admission across reloads.
        public const int EventContractVersion = 2;
        public static bool IsAvailable { get { return ErenshorJournalPlugin.Instance != null; } }

        private const int MaximumPendingEntries = 256;
        private static readonly Queue<PendingChronicleEntry> Pending = new Queue<PendingChronicleEntry>();
        private static readonly HashSet<string> PendingStableEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool AddChronicleEntry(string source, string category, string text)
        {
            return QueueChronicle(string.Empty, source, category, string.Empty, text);
        }

        /// <summary>
        /// Adds a structured Chronicle event with a stable source-owned id. Journal treats
        /// (source,eventId) as an exactly-once key for this character even after save/reload.
        /// The event id must identify the actual source event, not a display string or timestamp.
        /// </summary>
        public static bool AddChronicleEvent(string eventId, string source, string category, string title, string text)
        {
            string cleanEventId = JournalCore.CleanChronicleEventId(eventId);
            if (cleanEventId.Length == 0) return false;
            return QueueChronicle(cleanEventId, source, category, title, text);
        }

        private static bool QueueChronicle(string eventId, string source, string category, string title, string text)
        {
            if (!IsAvailable) return false;
            ErenshorJournalPlugin plugin = ErenshorJournalPlugin.Instance;
            string characterKey = plugin == null ? string.Empty : plugin.ControlCharacterKey;
            if (string.IsNullOrWhiteSpace(characterKey)) return false;

            string cleanText = JournalCore.CleanChronicleText(text);
            if (cleanText.Length == 0) return false;

            PendingChronicleEntry entry = new PendingChronicleEntry();
            entry.TimestampUtc = DateTime.UtcNow;
            entry.CharacterKey = characterKey;
            entry.EventId = JournalCore.CleanChronicleEventId(eventId);
            entry.Source = JournalCore.CleanChronicleLabel(source, JournalCore.MaxChronicleSourceLength);
            entry.Category = JournalCore.CleanChronicleLabel(category, JournalCore.MaxChronicleCategoryLength);
            entry.Title = JournalCore.CleanChronicleLabel(title, JournalCore.MaxChronicleTitleLength);
            if (entry.Title.Length == 0) entry.Title = JournalCore.ResolveChronicleTitle(entry.Source, entry.Category, cleanText);
            entry.Text = cleanText;

            lock (Pending)
            {
                if (Pending.Count >= MaximumPendingEntries) return false;
                string stableKey = StablePendingKey(entry.CharacterKey, entry.Source, entry.EventId);
                if (stableKey.Length > 0 && !PendingStableEvents.Add(stableKey)) return false;
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
                string stableKey = StablePendingKey(entry == null ? string.Empty : entry.CharacterKey,
                    entry == null ? string.Empty : entry.Source,
                    entry == null ? string.Empty : entry.EventId);
                if (stableKey.Length > 0) PendingStableEvents.Remove(stableKey);
                return true;
            }
        }

        internal static void ClearPending()
        {
            lock (Pending)
            {
                Pending.Clear();
                PendingStableEvents.Clear();
            }
        }

        private static string StablePendingKey(string characterKey, string source, string eventId)
        {
            if (string.IsNullOrWhiteSpace(characterKey) || string.IsNullOrWhiteSpace(eventId)) return string.Empty;
            return characterKey + "\u001f" + (source ?? string.Empty) + "\u001e" + eventId;
        }
    }

    internal sealed class PendingChronicleEntry
    {
        internal DateTime TimestampUtc;
        internal string CharacterKey;
        internal string EventId;
        internal string Source;
        internal string Category;
        internal string Title;
        internal string Text;
    }
}
