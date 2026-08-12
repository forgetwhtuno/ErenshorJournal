using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorJournal
{
    internal sealed class JournalStore
    {
        private const string Header = "ERENSHOR_JOURNAL_V1";
        private const long MaximumFileBytes = 32L * 1024L * 1024L;
        private readonly string _path;

        internal JournalStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A journal path is required.", "path");
            _path = path;
        }

        internal string PathOnDisk
        {
            get { return _path; }
        }

        internal JournalDocument Load(out string warning)
        {
            warning = null;
            if (!File.Exists(_path)) return JournalCore.CreateDefault();

            try
            {
                FileInfo info = new FileInfo(_path);
                if (info.Length > MaximumFileBytes) throw new InvalidDataException("Journal data file is unexpectedly large.");

                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                    throw new InvalidDataException("Journal data header is invalid.");

                JournalDocument document = new JournalDocument();
                document.Tabs.Clear();
                document.Chronicle.Clear();

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length == 0) continue;

                    if (string.Equals(parts[0], "SELECTED", StringComparison.Ordinal) && parts.Length >= 2)
                    {
                        int selected;
                        if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out selected))
                            document.SelectedTabIndex = selected;
                    }
                    else if (string.Equals(parts[0], "TAB", StringComparison.Ordinal) && parts.Length >= 4)
                    {
                        JournalTab tab = new JournalTab();
                        tab.Id = Decode(parts[1]);
                        tab.Name = Decode(parts[2]);
                        tab.Text = Decode(parts[3]);
                        document.Tabs.Add(tab);
                    }
                    else if (string.Equals(parts[0], "CHRON", StringComparison.Ordinal) && parts.Length >= 5)
                    {
                        long ticks;
                        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) continue;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) continue;

                        JournalChronicleEntry entry = new JournalChronicleEntry();
                        entry.TimestampUtc = new DateTime(ticks, DateTimeKind.Utc);
                        entry.Source = Decode(parts[2]);
                        entry.Category = Decode(parts[3]);
                        entry.Text = Decode(parts[4]);
                        document.Chronicle.Add(entry);
                    }
                }

                JournalCore.Normalize(document);
                return document;
            }
            catch (Exception ex)
            {
                warning = "The journal data file could not be read and was preserved as a .corrupt backup. " + ex.GetType().Name + ": " + ex.Message;
                PreserveCorruptFile();
                return JournalCore.CreateDefault();
            }
        }

        internal void Save(JournalDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            JournalCore.Normalize(document);

            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = _path + ".tmp";
            string backup = _path + ".bak";

            using (StreamWriter writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(Header);
                writer.WriteLine("SELECTED\t" + document.SelectedTabIndex.ToString(CultureInfo.InvariantCulture));

                for (int i = 0; i < document.Tabs.Count; i++)
                {
                    JournalTab tab = document.Tabs[i];
                    writer.WriteLine("TAB\t" + Encode(tab.Id) + "\t" + Encode(tab.Name) + "\t" + Encode(tab.Text));
                }

                for (int i = 0; i < document.Chronicle.Count; i++)
                {
                    JournalChronicleEntry entry = document.Chronicle[i];
                    writer.WriteLine("CHRON\t" + entry.TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "\t" +
                                     Encode(entry.Source) + "\t" + Encode(entry.Category) + "\t" + Encode(entry.Text));
                }
            }

            if (!File.Exists(_path))
            {
                File.Move(temp, _path);
                return;
            }

            try
            {
                File.Replace(temp, _path, backup, true);
            }
            catch
            {
                File.Copy(_path, backup, true);
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
        }

        private void PreserveCorruptFile()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string corrupt = _path + ".corrupt-" + stamp;
                int suffix = 2;
                while (File.Exists(corrupt))
                {
                    corrupt = _path + ".corrupt-" + stamp + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }
                File.Copy(_path, corrupt, true);
            }
            catch
            {
                // Never make recovery failure prevent the journal from opening.
            }
        }

        private static string Encode(string value)
        {
            if (value == null) value = string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
