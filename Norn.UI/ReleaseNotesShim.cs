namespace Norn.UI;

/// <summary>
/// Resolves <c>ReleaseNotes.md</c>'s path and loads <see cref="ReleaseNotesStore"/>.
/// Same shape as <see cref="HelpShim"/> — no per-user override, just the
/// bundled copy shipped next to the executable (<see cref="AppContext.BaseDirectory"/>,
/// see <c>ReleaseNotes.md</c>'s own <c>CopyToOutputDirectory</c> entry in
/// <c>Norn.UI.csproj</c>).
/// </summary>
public static class ReleaseNotesShim
{
    private const string FileName = "ReleaseNotes.md";

    public static void Load()
    {
        ReleaseNotesStore.Load(Path.Combine(AppContext.BaseDirectory, "Content", FileName));
    }
}
