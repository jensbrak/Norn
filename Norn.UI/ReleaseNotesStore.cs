namespace Norn.UI;

/// <summary>
/// Holds <c>ReleaseNotes.md</c>, parsed once at startup — same shape as
/// <see cref="HelpStore"/> (read-only, no save-side counterpart, graceful
/// degradation on a missing/corrupt file), sharing its parsing via
/// <see cref="MarkdownSectionParser"/>, but kept as its own store rather
/// than folded into <see cref="HelpStore"/>: <see cref="WelcomeWindow"/>
/// only ever wants the one section whose title matches the running
/// version (<see cref="ForVersion"/>), not every section browsable the way
/// <see cref="HelpWindow"/> shows <c>HelpContent.md</c>. Each <c>## x.y.z</c>
/// heading in the file is expected to match an <see cref="AppInfo.Version"/>
/// value exactly.
/// </summary>
public static class ReleaseNotesStore
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

    /// <summary>The section for <paramref name="version"/>, or <c>null</c>
    /// if the current build's version has no entry yet — <see cref="WelcomeWindow"/>
    /// falls back to its own placeholder text in that case, same as before
    /// this store existed.</summary>
    public static MarkdownSection? ForVersion(string version) =>
        Sections.FirstOrDefault(s => s.Title == version);
}
