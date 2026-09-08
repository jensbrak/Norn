using System.Text;

namespace Norn.Tests;

/// <summary>
/// Turns a byte-identity failure into something you can act on without opening a
/// hex editor.
/// </summary>
/// <remarks>
/// This is not decoration. R1 says two byte arrays differ; on its own that tells
/// you nothing about which field went wrong. The first differing offset, read
/// against the read order documented in FORMAT.md, names the field —
/// which is what makes deepening a parse layer a minutes-long loop
/// rather than a bisect.
/// </remarks>
internal static class ByteDiff
{
    private const int ContextBytes = 16;

    /// <summary>
    /// <c>null</c> when the arrays are identical; otherwise a report naming the
    /// first differing offset and showing both sides around it.
    /// </summary>
    internal static string? Describe(byte[] expected, byte[] actual)
    {
        var shared = Math.Min(expected.Length, actual.Length);

        var offset = 0;
        while (offset < shared && expected[offset] == actual[offset])
        {
            offset++;
        }

        if (offset == shared && expected.Length == actual.Length)
        {
            return null;
        }

        var report = new StringBuilder();

        if (offset == shared)
        {
            report.AppendLine(
                $"All {shared} shared bytes agree; the lengths differ. "
                + "A trailing region is missing or extra, not a mis-parsed field.");
        }
        else
        {
            report.AppendLine(
                $"First difference at offset {offset} (0x{offset:X8}): "
                + $"expected 0x{expected[offset]:X2}, got 0x{actual[offset]:X2}.");
        }

        report.AppendLine($"Lengths: expected {expected.Length}, got {actual.Length}.");

        var start = Math.Max(0, offset - ContextBytes);
        var end = Math.Min(Math.Max(expected.Length, actual.Length), offset + ContextBytes + 1);

        report.AppendLine($"Window from offset {start} (0x{start:X8}), '--' means past end of array:");
        report.AppendLine("  expected  " + Hex(expected, start, end));
        report.AppendLine("  actual    " + Hex(actual, start, end));
        report.AppendLine("            " + Caret(start, offset));

        return report.ToString();
    }

    private static string Hex(byte[] data, int start, int end)
    {
        var line = new StringBuilder();

        for (var i = start; i < end; i++)
        {
            if (i > start) line.Append(' ');
            line.Append(i < data.Length ? data[i].ToString("X2") : "--");
        }

        return line.ToString();
    }

    // Each byte renders as two hex digits and a separating space, so the caret
    // for byte n sits at column 3n.
    private static string Caret(int start, int offset)
    {
        return new string(' ', (offset - start) * 3) + "^^";
    }
}
