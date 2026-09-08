namespace Norn.UI;

/// <summary>
/// Where Valheim keeps world saves, per platform — sibling to
/// <see cref="SaveDirectoryResolver"/>, same pure-function shape and
/// reasoning: a function over <c>(platform, homeDirectory)</c>,
/// no environment reads, no filesystem access, so both platforms' rules are
/// testable from either host. Same base directory as character saves, one
/// leaf name swapped: <c>worlds</c>/<c>worlds_local</c> instead of
/// <c>characters</c>/<c>characters_local</c>.
/// </summary>
public static class WorldDirectoryResolver
{
    private const string WorldsFolder = "worlds";
    private const string WorldsLocalFolder = "worlds_local";

    /// <summary>The primary (cloud-eligible) world directory. Not scanned by
    /// <see cref="WorldIdentityShim"/> — see <see cref="ResolveLocalWorldDirectory"/>'s
    /// remarks — but kept, tested, and callable for a future, more deliberate
    /// cloud-support pass, same reasoning <see cref="SaveDirectoryResolver.ResolveSaveDirectory"/>
    /// already documents.</summary>
    public static string ResolveWorldDirectory(PlatformKind platform, string homeDirectory)
        => ValheimPathResolver.Resolve(platform, homeDirectory, WorldsFolder);

    /// <summary>The sibling local-saves directory. "Local" is the
    /// special-cased folder name, not the default, on both platforms — same
    /// as <see cref="SaveDirectoryResolver.ResolveLocalSaveDirectory"/>.</summary>
    public static string ResolveLocalWorldDirectory(PlatformKind platform, string homeDirectory)
        => ValheimPathResolver.Resolve(platform, homeDirectory, WorldsLocalFolder);
}
