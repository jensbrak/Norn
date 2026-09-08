namespace Norn.UI;

/// <summary>
/// Where Valheim keeps character saves, per platform. A pure function over
/// <c>(platform, homeDirectory)</c> — no environment reads, no filesystem
/// access — so both platforms' rules are testable from either host.
/// </summary>
public static class SaveDirectoryResolver
{
    private const string CharactersFolder = "characters";
    private const string CharactersLocalFolder = "characters_local";

    /// <summary>The primary save directory.</summary>
    public static string ResolveSaveDirectory(PlatformKind platform, string homeDirectory)
        => ValheimPathResolver.Resolve(platform, homeDirectory, CharactersFolder);

    /// <summary>
    /// The sibling local-saves directory. "Local" is the special-cased folder
    /// name, not the default, on both platforms.
    /// </summary>
    public static string ResolveLocalSaveDirectory(PlatformKind platform, string homeDirectory)
        => ValheimPathResolver.Resolve(platform, homeDirectory, CharactersLocalFolder);
}
