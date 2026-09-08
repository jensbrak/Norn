using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Norn.UI;

/// <summary>
/// Finds Valheim's Steam-Cloud-synced world files — a separate storage tier
/// from <see cref="WorldDirectoryResolver"/>'s home-directory-relative
/// paths, mirrored locally by the Steam client itself under
/// <c>userdata/&lt;account&gt;/892970/remote/worlds</c>
/// Norn's first registry read
/// (Windows only) — the environment-gated half; <see cref="SteamPathResolver"/>
/// holds the pure per-platform rule this hands data to.
/// </summary>
public static class SteamCloudWorldShim
{
    private const string ValheimAppId = "892970";

    /// <summary>
    /// Every locally-mirrored Steam Cloud "worlds" folder found, one per
    /// Steam account present under <c>userdata</c> — deliberately checks
    /// every account rather than trying to detect "the active" one (which
    /// would need Steam currently running and its own registry state to be
    /// reliable); harmless to check all of them since this is read-only.
    /// Empty when Steam isn't installed, has no <c>userdata</c>, or (on
    /// Linux) the hardcoded default path doesn't exist.
    /// </summary>
    public static IReadOnlyList<string> FindCloudWorldDirectories()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var registrySteamPath = OperatingSystem.IsWindows() ? ReadSteamPathFromRegistry() : null;

        var steamPath = SteamPathResolver.ResolveSteamInstallPath(platform, registrySteamPath, home);
        if (steamPath is null)
        {
            return [];
        }

        return FindCloudWorldDirectoriesUnder(steamPath);
    }

    /// <summary>
    /// The testable core: given a Steam install path (already resolved,
    /// no environment reads here), enumerates every <c>userdata/*</c>
    /// account folder and returns each one's <c>892970/remote/worlds</c>
    /// path that actually exists. Public, matching every other "given
    /// directories, do X" testable core in <c>Norn.UI</c> (<see cref="WorldFileLocator.FindWorldFiles"/>,
    /// <see cref="SaveFileLocator.FindSaveFiles"/>) — no new <c>InternalsVisibleTo</c>
    /// grant needed for this alone.
    /// </summary>
    public static IReadOnlyList<string> FindCloudWorldDirectoriesUnder(string steamPath)
    {
        var userDataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userDataPath))
        {
            return [];
        }

        var directories = new List<string>();
        foreach (var accountDirectory in Directory.EnumerateDirectories(userDataPath))
        {
            var worldsDirectory = Path.Combine(accountDirectory, ValheimAppId, "remote", "worlds");
            if (Directory.Exists(worldsDirectory))
            {
                directories.Add(worldsDirectory);
            }
        }

        return directories;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadSteamPathFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            // Missing key, access denied, or any other registry-read
            // failure: Steam's path just isn't discoverable this way —
            // a handled case (falls through to "no Cloud enrichment this
            // run"), not an error worth surfacing.
            return null;
        }
    }
}
