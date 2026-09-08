using System.Globalization;
using CsvHelper;

namespace Norn.Adapter;

/// <summary>
/// Norn's seam onto the localization-string export —
/// a flat "Name,Text" mapping from a Valheim localization key (e.g.
/// <c>piece_forge</c>) to its resolved English display text. Same
/// static/load-once shape as <see cref="SharedItemDataCatalog"/> and the
/// same reasons (no DI container; a long-lived catalog with an explicit
/// reload point fits a static better than threading an instance through
/// every caller). Uses CsvHelper rather than a hand-rolled reader — the
/// source file contains multiline quoted values, which a line-by-line
/// reader (as <see cref="SharedItemDataCatalog"/> used before its own
/// CsvHelper migration) cannot parse correctly: a quoted field crossing a
/// physical newline needs a real character-stream parser, not a
/// per-line split.
/// </summary>
public static class LocalizationCatalog
{
    private static IReadOnlyDictionary<string, string> _entries = new Dictionary<string, string>();

    /// <summary>Number of resolved entries.</summary>
    public static int Count => _entries.Count;

    /// <summary>
    /// Loads from the first existing, parseable path in
    /// <paramref name="candidatePaths"/>, in order — same
    /// bundled-default/external-override precedence contract as
    /// <see cref="SharedItemDataCatalog.Load"/>. Leaves the catalog empty,
    /// not an error, if no candidate exists or every candidate fails to
    /// parse: a missing or corrupt localization file degrades every
    /// resolved-name display back to its raw key rather than blocking the
    /// app.
    /// </summary>
    public static void Load(params string?[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (path is null || !File.Exists(path))
            {
                continue;
            }

            try
            {
                _entries = ParseFile(path);
                return;
            }
            catch
            {
                // Corrupt or unreadable candidate: try the next one, or fall
                // through to empty below — same graceful-degradation contract
                // as SharedItemDataCatalog.
            }
        }

        _entries = new Dictionary<string, string>();
    }

    /// <summary>
    /// Resolves a Valheim localization key to its English text, or
    /// <c>null</c> on a miss — a handled case (unresolved key), not an
    /// error. Accepts the key with or without the leading <c>$</c> the wire
    /// format itself uses (e.g. <c>Player.m_knownStations</c>'s keys) — the
    /// CSV's own <c>Name</c> column never carries it, so the <c>$</c> is
    /// stripped here rather than requiring every caller to know that.
    /// </summary>
    public static string? TryFind(string key)
    {
        var normalized = key.StartsWith('$') ? key[1..] : key;
        return _entries.TryGetValue(normalized, out var text) ? text : null;
    }

    private static IReadOnlyDictionary<string, string> ParseFile(string path)
    {
        var result = new Dictionary<string, string>();

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var name = csv.GetField("Name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result[name] = csv.GetField("Text") ?? ""; // last occurrence wins on a duplicate name
        }

        return result;
    }
}
