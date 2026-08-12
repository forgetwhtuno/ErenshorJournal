using System;
using System.Collections.Generic;

namespace ErenshorJournal
{
    public sealed class JournalDocument
    {
        public int Version = 1;
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
        public string Source = string.Empty;
        public string Category = string.Empty;
        public string Text = string.Empty;
    }

    internal static class JournalCore
    {
        internal const int MaxTabs = 32;
        internal const int MaxChronicleEntries = 2000;
        internal const int MaxTabNameLength = 40;

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
            {
                JournalTab tab = document.Tabs[i];
                if (tab == null)
                {
                    document.Tabs.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tab.Id)) tab.Id = Guid.NewGuid().ToString("N");
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

                if (entry.TimestampUtc == default(DateTime)) entry.TimestampUtc = DateTime.UtcNow;
                if (entry.TimestampUtc.Kind != DateTimeKind.Utc) entry.TimestampUtc = entry.TimestampUtc.ToUniversalTime();
                if (entry.Source == null) entry.Source = string.Empty;
                if (entry.Category == null) entry.Category = string.Empty;
                if (entry.Text == null) entry.Text = string.Empty;
            }

            TrimChronicle(document);
            document.Version = 1;
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

        internal static void AppendChronicle(JournalDocument document, string source, string category, string text, DateTime timestampUtc)
        {
            Normalize(document);
            if (string.IsNullOrWhiteSpace(text)) return;

            JournalChronicleEntry entry = new JournalChronicleEntry();
            entry.TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
            entry.Source = source == null ? string.Empty : source.Trim();
            entry.Category = category == null ? string.Empty : category.Trim();
            entry.Text = text.Trim();
            document.Chronicle.Add(entry);
            TrimChronicle(document);
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
