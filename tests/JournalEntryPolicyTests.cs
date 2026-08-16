using System;
using ErenshorJournal;

internal static class JournalEntryPolicyTests
{
    private static int _passed;

    private static void Main()
    {
        Run("empty note starts directly with marker", TestEmpty);
        Run("existing text gets one blank line before marker", TestExistingText);
        Run("one trailing newline becomes one blank line", TestSingleNewline);
        Run("existing blank line is not expanded", TestExistingBlankLine);
        Run("LF-only imported text remains well separated", TestLfOnly);
        Console.WriteLine("PASS: " + _passed + " tests");
    }

    private static DateTime Stamp { get { return new DateTime(2026, 8, 14, 1, 23, 0, DateTimeKind.Local); } }

    private static void TestEmpty()
    {
        Assert(JournalEntryPolicy.AppendTimestampMarker(string.Empty, Stamp, "\r\n") == "[2026-08-14 01:23] ", "empty marker format changed");
    }

    private static void TestExistingText()
    {
        Assert(JournalEntryPolicy.AppendTimestampMarker("camp notes", Stamp, "\r\n") == "camp notes\r\n\r\n[2026-08-14 01:23] ", "existing note separator changed");
    }

    private static void TestSingleNewline()
    {
        Assert(JournalEntryPolicy.AppendTimestampMarker("camp notes\r\n", Stamp, "\r\n") == "camp notes\r\n\r\n[2026-08-14 01:23] ", "single trailing newline should become a blank line");
    }

    private static void TestExistingBlankLine()
    {
        Assert(JournalEntryPolicy.AppendTimestampMarker("camp notes\r\n\r\n", Stamp, "\r\n") == "camp notes\r\n\r\n[2026-08-14 01:23] ", "existing blank line should be preserved exactly");
    }

    private static void TestLfOnly()
    {
        Assert(JournalEntryPolicy.AppendTimestampMarker("imported\n", Stamp, "\r\n") == "imported\n\r\n[2026-08-14 01:23] ", "LF-only imported note should remain separated");
    }

    private static void Run(string name, Action test)
    {
        test(); _passed++; Console.WriteLine("PASS: " + name);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
