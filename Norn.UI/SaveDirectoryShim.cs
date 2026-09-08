namespace Norn.UI;

/// <summary>
/// Reads the running platform (via <see cref="PlatformDetection"/>) and the
/// home directory, then hands both to the pure
/// <see cref="SaveDirectoryResolver"/>. One of several small platform-gated
/// shims, not the only one — corrected from an earlier claim to be unique,
/// which was already false when written (found in review).
/// </summary>
/// <remarks>
/// Deliberately scans only <see cref="SaveDirectoryResolver.ResolveLocalSaveDirectory"/>,
/// not the Steam-Cloud-eligible <c>characters</c> directory
/// (<see cref="SaveDirectoryResolver.ResolveSaveDirectory"/>) — the
/// "local saves only" rule is sharpened here from "Norn doesn't call any
/// Steam Cloud API" to "Norn doesn't surface files sitting in the
/// cloud-eligible folder at all," since their true sync state can't be known
/// from outside the game and editing one risks being silently clobbered by,
/// or clobbering, the cloud copy. The primary
/// resolver is kept, tested, and callable — this is a deliberate narrowing of
/// what the shim aggregates, not a removal of the underlying capability, so a
/// future, more deliberate cloud-support pass has it ready to reuse.
/// </remarks>
public static class SaveDirectoryShim
{
    /// <summary>The local (non-cloud) save directory for the running platform.</summary>
    public static IReadOnlyList<string> ResolveSaveDirectories()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return [SaveDirectoryResolver.ResolveLocalSaveDirectory(platform, home)];
    }
}
