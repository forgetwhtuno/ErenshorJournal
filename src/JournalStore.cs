using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorJournal
{
    internal sealed class JournalStore
    {
        private const string HeaderV1 = "ERENSHOR_JOURNAL_V1";
        private const string HeaderV2 = "ERENSHOR_JOURNAL_V2";
        private const long MaximumFileBytes = 32L * 1024L * 1024L;
        private readonly string _path;

        internal JournalStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A journal path is required.", "path");
            _path = path;
        }

        internal string PathOnDisk { get { return _path; } }

        internal JournalDocument Load(out string warning)
        {
            warning = null;
            string backup = _path + ".bak";
            string temp = _path + ".tmp";

            if (!File.Exists(_path))
            {
                JournalDocument recoveredMissing;
                int recoveredMissingSkipped;
                bool recoveredMissingFromTemp;
                if (TryLoadNewestRecovery(backup, temp, out recoveredMissing, out recoveredMissingSkipped, out recoveredMissingFromTemp))
                {
                    int duplicates = JournalCore.RemoveExactChronicleDuplicates(recoveredMissing);
                    if (recoveredMissingFromTemp)
                    {
                        warning = recoveredMissingSkipped > 0 || duplicates > 0
                            ? "The main journal file was missing; the newest readable interrupted-save file was recovered with some malformed or duplicate records ignored."
                            : "The main journal file was missing; the newest readable interrupted-save file was recovered.";
                    }
                    else
                    {
                        warning = recoveredMissingSkipped > 0 || duplicates > 0
                            ? "The main journal file was missing; a readable local backup was recovered with some malformed or duplicate records ignored."
                            : "The main journal file was missing; the local backup was recovered.";
                    }
                    return recoveredMissing;
                }
                return JournalCore.CreateDefault();
            }

            JournalDocument document;
            int skippedRecords;
            if (TryLoadFile(_path, out document, out skippedRecords))
            {
                // A complete temp can legitimately coexist with an older readable main if the
                // process died after the durable temp flush but before File.Replace/Move. Prefer
                // that candidate only when it is itself structurally valid and at least as new as
                // the main file. Partial/incomplete temps fail TryLoadFile and can never override it.
                JournalDocument newerTemp;
                int newerTempSkipped;
                if (IsAtLeastAsNew(temp, _path) && TryLoadFile(temp, out newerTemp, out newerTempSkipped))
                {
                    int tempDuplicates = JournalCore.RemoveExactChronicleDuplicates(newerTemp);
                    warning = newerTempSkipped > 0 || tempDuplicates > 0
                        ? "A newer interrupted save was recovered from a readable temporary file with some malformed or duplicate records ignored."
                        : "A newer interrupted save was recovered from a readable temporary file.";
                    return newerTemp;
                }

                int duplicates = JournalCore.RemoveExactChronicleDuplicates(document);
                if (skippedRecords > 0 || duplicates > 0)
                {
                    warning = "Some malformed or duplicate local journal records were ignored; the readable notes were preserved.";
                }
                return document;
            }

            PreserveCorruptFile();

            JournalDocument recovered;
            int recoveredSkipped;
            bool recoveredFromTemp;
            if (TryLoadNewestRecovery(backup, temp, out recovered, out recoveredSkipped, out recoveredFromTemp))
            {
                int recoveredDuplicates = JournalCore.RemoveExactChronicleDuplicates(recovered);
                if (recoveredFromTemp)
                {
                    warning = recoveredSkipped > 0 || recoveredDuplicates > 0
                        ? "The main journal file was unreadable and was preserved; the newest readable interrupted-save file was recovered with some malformed or duplicate records ignored."
                        : "The main journal file was unreadable and was preserved; the newest readable interrupted-save file was recovered.";
                }
                else
                {
                    warning = recoveredSkipped > 0 || recoveredDuplicates > 0
                        ? "The main journal file was unreadable and was preserved; a readable local backup was recovered with some malformed or duplicate records ignored."
                        : "The main journal file was unreadable and was preserved; the local backup was recovered.";
                }
                return recovered;
            }

            warning = "The journal data file could not be read and was preserved as a .corrupt backup. A fresh journal was opened.";
            return JournalCore.CreateDefault();
        }

        internal void Save(JournalDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            JournalCore.Normalize(document);

            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = _path + ".tmp";
            string backup = _path + ".bak";

            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.WriteLine(HeaderV2);
                writer.WriteLine("SELECTED\t" + document.SelectedTabIndex.ToString(CultureInfo.InvariantCulture));

                for (int i = 0; i < document.Tabs.Count; i++)
                {
                    JournalTab tab = document.Tabs[i];
                    writer.WriteLine("TAB\t" + Encode(tab.Id) + "\t" + Encode(tab.Name) + "\t" + Encode(tab.Text));
                }

                for (int i = 0; i < document.Chronicle.Count; i++)
                {
                    JournalChronicleEntry entry = document.Chronicle[i];
                    writer.WriteLine("CHRON2\t" + entry.TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "\t" +
                                     Encode(entry.EventId) + "\t" + Encode(entry.Source) + "\t" + Encode(entry.Category) + "\t" +
                                     Encode(entry.Title) + "\t" + Encode(entry.Text));
                }
                writer.Flush();
                stream.Flush(true);
            }

            FileInfo tempInfo = new FileInfo(temp);
            if (tempInfo.Length > MaximumFileBytes)
            {
                try { File.Delete(temp); } catch { }
                throw new InvalidDataException("Journal data exceeds the supported local file size.");
            }

            if (!File.Exists(_path))
            {
                File.Move(temp, _path);
                return;
            }

            try
            {
                // On normal Windows/NTFS installs this is the atomic path: destination is replaced
                // in one filesystem operation and the previous good file becomes .bak.
                File.Replace(temp, _path, backup, true);
            }
            catch
            {
                // Some filesystems do not support File.Replace. Preserve a full known-good backup
                // before the compatibility copy; if that copy is interrupted, Load() recovers it.
                File.Copy(_path, backup, true);
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
        }

        private static bool TryLoadFile(string path, out JournalDocument document, out int skippedRecords)
        {
            document = null;
            skippedRecords = 0;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaximumFileBytes) return false;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length == 0) return false;
                bool v1 = string.Equals(lines[0], HeaderV1, StringComparison.Ordinal);
                bool v2 = string.Equals(lines[0], HeaderV2, StringComparison.Ordinal);
                if (!v1 && !v2) return false;

                JournalDocument loaded = new JournalDocument();
                loaded.Tabs.Clear();
                loaded.Chronicle.Clear();
                int validTabs = 0;

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length == 0) continue;

                    if (string.Equals(parts[0], "SELECTED", StringComparison.Ordinal))
                    {
                        int selected;
                        if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out selected))
                        {
                            skippedRecords++;
                            continue;
                        }
                        loaded.SelectedTabIndex = selected;
                    }
                    else if (string.Equals(parts[0], "TAB", StringComparison.Ordinal))
                    {
                        string id, name, text;
                        if (parts.Length < 4 || !TryDecode(parts[1], out id) || !TryDecode(parts[2], out name) || !TryDecode(parts[3], out text))
                        {
                            skippedRecords++;
                            continue;
                        }
                        JournalTab tab = new JournalTab();
                        tab.Id = id; tab.Name = name; tab.Text = text;
                        loaded.Tabs.Add(tab);
                        validTabs++;
                    }
                    else if (string.Equals(parts[0], "CHRON2", StringComparison.Ordinal))
                    {
                        long ticks;
                        string eventId, source, category, title, text;
                        if (parts.Length < 7 ||
                            !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                            ticks <= DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ||
                            !TryDecode(parts[2], out eventId) || !TryDecode(parts[3], out source) ||
                            !TryDecode(parts[4], out category) || !TryDecode(parts[5], out title) || !TryDecode(parts[6], out text))
                        {
                            skippedRecords++;
                            continue;
                        }
                        JournalChronicleEntry entry = new JournalChronicleEntry();
                        entry.TimestampUtc = new DateTime(ticks, DateTimeKind.Utc);
                        entry.EventId = eventId; entry.Source = source; entry.Category = category; entry.Title = title; entry.Text = text;
                        loaded.Chronicle.Add(entry);
                    }
                    else if (string.Equals(parts[0], "CHRON", StringComparison.Ordinal))
                    {
                        // V1 compatibility: preserve every readable legacy Chronicle row and let
                        // JournalCore normalize its missing EventId/Title fields in memory.
                        long ticks;
                        string source, category, text;
                        if (parts.Length < 5 ||
                            !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                            ticks <= DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ||
                            !TryDecode(parts[2], out source) || !TryDecode(parts[3], out category) || !TryDecode(parts[4], out text))
                        {
                            skippedRecords++;
                            continue;
                        }
                        JournalChronicleEntry entry = new JournalChronicleEntry();
                        entry.TimestampUtc = new DateTime(ticks, DateTimeKind.Utc);
                        entry.Source = source; entry.Category = category; entry.Title = string.Empty; entry.Text = text;
                        loaded.Chronicle.Add(entry);
                    }
                    else
                    {
                        skippedRecords++;
                    }
                }

                // Every file this version has ever successfully saved contains at least one TAB.
                // Header-only/truncated files therefore fail closed and are eligible for .bak recovery.
                if (validTabs == 0) return false;
                JournalCore.Normalize(loaded);
                document = loaded;
                return true;
            }
            catch
            {
                document = null;
                skippedRecords = 0;
                return false;
            }
        }

        private static bool TryLoadNewestRecovery(string backup, string temp, out JournalDocument document,
            out int skippedRecords, out bool fromTemp)
        {
            document = null;
            skippedRecords = 0;
            fromTemp = false;

            JournalDocument backupDocument;
            int backupSkipped;
            bool backupReadable = TryLoadFile(backup, out backupDocument, out backupSkipped);

            JournalDocument tempDocument;
            int tempSkipped;
            bool tempReadable = TryLoadFile(temp, out tempDocument, out tempSkipped);

            // A surviving complete temp is the newest fully-written transaction when it is at
            // least as new as the backup. This matters on filesystems where File.Replace is not
            // supported and a process dies during the compatibility copy into the live path.
            if (tempReadable && (!backupReadable || IsAtLeastAsNew(temp, backup)))
            {
                document = tempDocument;
                skippedRecords = tempSkipped;
                fromTemp = true;
                return true;
            }

            if (backupReadable)
            {
                document = backupDocument;
                skippedRecords = backupSkipped;
                return true;
            }

            return false;
        }

        private static bool IsAtLeastAsNew(string candidate, string baseline)
        {
            try
            {
                if (!File.Exists(candidate) || !File.Exists(baseline)) return false;
                return File.GetLastWriteTimeUtc(candidate) >= File.GetLastWriteTimeUtc(baseline);
            }
            catch { return false; }
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

        private static bool TryDecode(string value, out string decoded)
        {
            decoded = string.Empty;
            if (string.IsNullOrEmpty(value)) return true;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                return true;
            }
            catch
            {
                decoded = string.Empty;
                return false;
            }
        }
    }
}
