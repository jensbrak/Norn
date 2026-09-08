namespace Norn.UI;

/// <summary>
/// Holds <c>HelpContent.md</c>, parsed once at startup. A static seam,
/// matching <c>Norn.Adapter</c>'s own catalog precedent — the content is
/// read-only from the app's own perspective (nothing in Norn ever writes
/// help text back), so there's no save-side counterpart the way
/// <see cref="SettingsStore"/> has. Shares its parsing with
/// <see cref="ReleaseNotesStore"/> (<see cref="MarkdownSectionParser"/>) but
/// stays its own store: <see cref="HelpWindow"/> shows every section,
/// browsable, while release notes only ever want the one section matching
/// the running version — different enough consumption that folding the two
/// stores together wouldn't buy anything.
/// </summary>
public static class HelpStore
{
    public static IReadOnlyList<MarkdownSection> Sections { get; private set; } = [];

    /// <summary>Missing or unreadable file degrades to an empty section
    /// list, not a startup failure — the same graceful-degradation posture
    /// every other external-file seam in this app already takes.</summary>
    public static void Load(string path)
    {
        try
        {
            Sections = File.Exists(path) ? MarkdownSectionParser.Parse(File.ReadAllText(path)) : [];
        }
        catch
        {
            Sections = [];
        }
    }
}
