namespace Norn.UI;

/// <summary>
/// Resolves <c>HelpContent.md</c>'s path and loads <see cref="HelpStore"/>.
/// Not actually platform-gated the way <see cref="ItemCatalogShim"/>/
/// <see cref="SettingsShim"/> are — this file has no per-user override, just
/// the bundled copy shipped next to the executable
/// (<see cref="AppContext.BaseDirectory"/>, identical cross-platform — see
/// <c>HelpContent.md</c>'s own <c>CopyToOutputDirectory</c> entry in
/// <c>Norn.UI.csproj</c>), so there's no platform branch to write. Kept as
/// its own small <c>Shim</c> anyway, called from <see cref="Program"/>
/// alongside the others, for the same reason every other startup subsystem
/// gets one: a reader scanning <c>Program.Main</c> sees one uniform list of
/// what loads before the window shows, not an exception to spot.
/// </summary>
public static class HelpShim
{
    private const string FileName = "HelpContent.md";

    public static void Load()
    {
        HelpStore.Load(Path.Combine(AppContext.BaseDirectory, "Content", FileName));
    }
}
