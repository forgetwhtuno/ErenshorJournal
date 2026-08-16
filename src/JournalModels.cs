using System;
using System.Collections.Generic;

namespace ErenshorJournal
{
    public sealed class JournalDocument
    {
        public int Version = 2;
        public int SelectedTabIndex = 0;
        public List<JournalTab> Tabs = new List<JournalTab>();
        public List<JournalChronicleEntry> Chronicle = new List<JournalChronicleEntry>();
    }

    public sealed class JournalTab
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Text = string.Empty;
    }

    public sealed class JournalChronicleEntry
    {
        public DateTime TimestampUtc;
        // Stable source-owned identity for exactly-once events. Legacy v1 Chronicle rows leave
        // this blank and continue using the bounded short-window content dedupe.
        public string EventId = string.Empty;
        public string Source = string.Empty;
        public string Category = string.Empty;
        public string Title = string.Empty;
        public string Text = string.Empty;
    }

    internal static class JournalCore
    {
        internal const int MaxTabs = 32;
        internal const int MaxChronicleEntries = 2000;
        internal const int MaxTabNameLength = 40;
        internal const int MaxChronicleSourceLength = 64;
        internal const int MaxChronicleCategoryLength = 64;
        internal const int MaxChronicleEventIdLength = 160;
        internal const int MaxChronicleTitleLength = 120;
        internal const int MaxChronicleTextLength = 1200;
        internal const int ChronicleDuplicateLookback = 32;
        internal const double ChronicleDuplicateWindowSeconds = 15.0;

        internal static JournalDocument CreateDefault()
        {
            JournalDocument document = new JournalDocument();
            document.Tabs.Add(NewTab("Journal"));
            document.Tabs.Add(NewTab("Quest Notes"));
            document.Tabs.Add(NewTab("Crafting"));
            document.SelectedTabIndex = 0;
            return document;
        }

        internal static void Normalize(JournalDocument document)
        {
            if (document == null) return;
            if (document.Tabs == null) document.Tabs = new List<JournalTab>();
            if (document.Chronicle == null) document.Chronicle = new List<JournalChronicleEntry>();

            for (int i = document.Tabs.Count - 1; i >= 0; i--)
                if (document.Tabs[i] == null) document.Tabs.RemoveAt(i);

            HashSet<string> tabIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.Tabs.Count; i++)
            {
                JournalTab tab = document.Tabs[i];
                string id = string.IsNullOrWhiteSpace(tab.Id) ? string.Empty : tab.Id.Trim();
                if (id.Length == 0 || tabIds.Contains(id))
                {
                    do { id = Guid.NewGuid().ToString("N"); } while (tabIds.Contains(id));
                }
                tab.Id = id;
                tabIds.Add(id);
                tab.Name = CleanTabName(tab.Name);
                if (tab.Text == null) tab.Text = string.Empty;
            }

            if (document.Tabs.Count == 0) document.Tabs.Add(NewTab("Journal"));
            while (document.Tabs.Count > MaxTabs) document.Tabs.RemoveAt(document.Tabs.Count - 1);

            if (document.SelectedTabIndex < 0) document.SelectedTabIndex = 0;
            if (document.SelectedTabIndex >= document.Tabs.Count) document.SelectedTabIndex = document.Tabs.Count - 1;

            for (int i = document.Chronicle.Count - 1; i >= 0; i--)
            {
                JournalChronicleEntry entry = document.Chronicle[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                {
                    document.Chronicle.RemoveAt(i);
                    continue;
                }

                entry.TimestampUtc = NormalizeUtc(entry.TimestampUtc);
                entry.EventId = CleanChronicleEventId(entry.EventId);
                entry.Source = CleanChronicleLabel(entry.Source, MaxChronicleSourceLength);
                entry.Category = CleanChronicleLabel(entry.Category, MaxChronicleCategoryLength);
                entry.Title = CleanChronicleLabel(entry.Title, MaxChronicleTitleLength);
                if (entry.Title.Length == 0) entry.Title = ResolveChronicleTitle(entry.Source, entry.Category, entry.Text);
                entry.Text = CleanChronicleText(entry.Text);
                if (entry.Text.Length == 0) document.Chronicle.RemoveAt(i);
            }

            TrimChronicle(document);
            document.Version = 2;
        }

        internal static bool AddTab(JournalDocument document)
        {
            Normalize(document);
            if (document.Tabs.Count >= MaxTabs) return false;

            string baseName = "New Tab";
            string name = baseName;
            int suffix = 2;
            while (ContainsTabName(document, name))
            {
                name = baseName + " " + suffix.ToString();
                suffix++;
            }

            document.Tabs.Add(NewTab(name));
            document.SelectedTabIndex = document.Tabs.Count - 1;
            return true;
        }

        internal static bool DeleteSelectedTab(JournalDocument document)
        {
            Normalize(document);
            if (document.Tabs.Count <= 1) return false;

            int index = document.SelectedTabIndex;
            document.Tabs.RemoveAt(index);
            if (index >= document.Tabs.Count) index = document.Tabs.Count - 1;
            document.SelectedTabIndex = index;
            return true;
        }

        internal static string CleanTabName(string value)
        {
            string cleaned = string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
            cleaned = cleaned.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            if (cleaned.Length > MaxTabNameLength) cleaned = cleaned.Substring(0, MaxTabNameLength);
            return cleaned;
        }

        internal static bool AppendChronicle(JournalDocument document, string source, string category, string text, DateTime timestampUtc)
        {
            // Compatibility surface for v1 callers. Without a source-owned event id, preserve the
            // existing bounded content/time dedupe so legitimate repeated events remain possible.
            return AppendChronicleEvent(document, string.Empty, source, category, string.Empty, text, timestampUtc);
        }

        internal static bool AppendChronicleEvent(JournalDocument document, string eventId, string source, string category,
            string title, string text, DateTime timestampUtc)
        {
            Normalize(document);
            string cleanText = CleanChronicleText(text);
            if (cleanText.Length == 0) return false;

            string cleanEventId = CleanChronicleEventId(eventId);
            string cleanSource = CleanChronicleLabel(source, MaxChronicleSourceLength);
            string cleanCategory = CleanChronicleLabel(category, MaxChronicleCategoryLength);
            string cleanTitle = CleanChronicleLabel(title, MaxChronicleTitleLength);
            if (cleanTitle.Length == 0) cleanTitle = ResolveChronicleTitle(cleanSource, cleanCategory, cleanText);
            DateTime cleanTimestamp = NormalizeUtc(timestampUtc);

            if (cleanEventId.Length > 0)
            {
                // Stable event ids are durable exactly-once keys within the source. Scan the full
                // retained Chronicle so a retry after save/reload can never recreate the same event.
                for (int i = document.Chronicle.Count - 1; i >= 0; i--)
                {
                    JournalChronicleEntry existing = document.Chronicle[i];
                    if (existing == null) continue;
                    if (!string.Equals(CleanChronicleLabel(existing.Source, MaxChronicleSourceLength), cleanSource, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(CleanChronicleEventId(existing.EventId), cleanEventId, StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            else
            {
                int start = Math.Max(0, document.Chronicle.Count - ChronicleDuplicateLookback);
                for (int i = document.Chronicle.Count - 1; i >= start; i--)
                {
                    JournalChronicleEntry existing = document.Chronicle[i];
                    if (existing == null) continue;
                    if (!string.Equals(existing.Source ?? string.Empty, cleanSource, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(existing.Category ?? string.Empty, cleanCategory, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(existing.Text ?? string.Empty, cleanText, StringComparison.Ordinal)) continue;
                    DateTime existingUtc = NormalizeUtc(existing.TimestampUtc);
                    if (Math.Abs((existingUtc - cleanTimestamp).TotalSeconds) <= ChronicleDuplicateWindowSeconds) return false;
                }
            }

            JournalChronicleEntry entry = new JournalChronicleEntry();
            entry.TimestampUtc = cleanTimestamp;
            entry.EventId = cleanEventId;
            entry.Source = cleanSource;
            entry.Category = cleanCategory;
            entry.Title = cleanTitle;
            entry.Text = cleanText;
            document.Chronicle.Add(entry);
            TrimChronicle(document);
            return true;
        }

        internal static int RemoveExactChronicleDuplicates(JournalDocument document)
        {
            if (document == null || document.Chronicle == null || document.Chronicle.Count < 2) return 0;
            int removed = 0;
            HashSet<string> stableEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> legacyExact = new HashSet<string>(StringComparer.Ordinal);
            for (int i = document.Chronicle.Count - 1; i >= 0; i--)
            {
                JournalChronicleEntry entry = document.Chronicle[i];
                if (entry == null) continue;
                string source = CleanChronicleLabel(entry.Source, MaxChronicleSourceLength);
                string eventId = CleanChronicleEventId(entry.EventId);
                if (eventId.Length > 0)
                {
                    string eventKey = source + "\u001f" + eventId;
                    if (!stableEvents.Add(eventKey))
                    {
                        document.Chronicle.RemoveAt(i);
                        removed++;
                    }
                    continue;
                }

                DateTime timestamp = NormalizeUtc(entry.TimestampUtc);
                string category = CleanChronicleLabel(entry.Category, MaxChronicleCategoryLength).ToUpperInvariant();
                string text = CleanChronicleText(entry.Text);
                string key = timestamp.Ticks.ToString() + "\u001f" + source.ToUpperInvariant() + "\u001e" + category + "\u001d" + text;
                if (!legacyExact.Add(key))
                {
                    document.Chronicle.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        internal static string CleanChronicleEventId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('\0', ' ').Trim();
            return clean.Length <= MaxChronicleEventIdLength ? clean : clean.Substring(0, MaxChronicleEventIdLength);
        }

        internal static string ResolveChronicleTitle(string source, string category, string text)
        {
            // Legacy v1 callers only provide source/category/body. Prefer the body's first concise
            // sentence as a useful event title (e.g. "Completed Global Contract: Grand Tour")
            // while source/category remain visible as provenance.
            string cleanText = CleanChronicleText(text);
            if (cleanText.Length > 0)
            {
                int breakAt = cleanText.IndexOfAny(new char[] { '\r', '\n', '.', '!', '?' });
                string title = breakAt > 0 ? cleanText.Substring(0, breakAt) : cleanText;
                title = CleanChronicleLabel(title, MaxChronicleTitleLength);
                if (title.Length > 0) return title;
            }
            string categoryTitle = CleanChronicleLabel(category, MaxChronicleTitleLength);
            if (categoryTitle.Length > 0) return categoryTitle;
            string sourceTitle = CleanChronicleLabel(source, MaxChronicleTitleLength);
            if (sourceTitle.Length > 0) return sourceTitle;
            return "Chronicle Entry";
        }

        internal static string CleanChronicleLabel(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || maxLength <= 0) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('\0', ' ').Trim();
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength);
        }

        internal static string CleanChronicleText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace("\0", string.Empty).Trim();
            return clean.Length <= MaxChronicleTextLength ? clean : clean.Substring(0, MaxChronicleTextLength);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime)) return DateTime.UtcNow;
            if (value.Kind == DateTimeKind.Utc) return value;
            try { return value.ToUniversalTime(); }
            catch { return DateTime.UtcNow; }
        }

        private static JournalTab NewTab(string name)
        {
            JournalTab tab = new JournalTab();
            tab.Id = Guid.NewGuid().ToString("N");
            tab.Name = CleanTabName(name);
            tab.Text = string.Empty;
            return tab;
        }

        private static bool ContainsTabName(JournalDocument document, string name)
        {
            for (int i = 0; i < document.Tabs.Count; i++)
            {
                JournalTab tab = document.Tabs[i];
                if (tab != null && string.Equals(tab.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void TrimChronicle(JournalDocument document)
        {
            int excess = document.Chronicle.Count - MaxChronicleEntries;
            if (excess > 0) document.Chronicle.RemoveRange(0, excess);
        }
    }
}
