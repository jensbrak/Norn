using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The platform-gated glue for the world-identity scan:
/// resolves every directory that might hold a local <c>.fwl</c>, finds
/// every non-backup one, and refreshes <see cref="WorldIdentityCatalog"/>.
/// Sibling to <see cref="ItemCatalogShim"/>/<see cref="LocalizationCatalogShim"/>,
/// but not called from <see cref="Program"/>: unlike those (small, bundled
/// CSVs, fast enough to load before any window exists), this scan reads an
/// unbounded number of files from outside Norn's own tree, so
/// <c>MainWindow</c> runs it on a background thread after the window is
/// already showing, with status-bar feedback either side.
/// <para>
/// The <em>character</em>-save scan (<see cref="SaveDirectoryShim"/>)
/// deliberately excludes the cloud-eligible folder entirely — but that
/// reasoning is about <em>write</em> safety
/// (Norn edits and saves character files), and Norn never writes a
/// <c>.fwl</c> at all. So this scan also reads Steam's own local Cloud
/// mirror (<see cref="SteamCloudWorldShim"/>) — a different, genuinely
/// local, read-only-safe folder, not the plain (non-<c>_local</c>)
/// <c>worlds</c> directory, which real inspection showed holds only stale
/// legacy leftovers for actual Cloud-tier worlds, not a live mirror.
/// </para>
/// <para>
/// The Cloud mirror specifically is skipped while <see cref="GameProcessShim.IsGameRunning"/>
/// — a softer justification than <see cref="GameRunningBanner"/>'s (that
/// guards an actual edit-conflict hazard; this avoids reading a file Steam's
/// own client might be mid-write on around Valheim's launch/exit, per
/// Steamworks' own documented Auto-Cloud sync timing). <b>Still a real
/// mitigation, not just politeness</b> — <see cref="GameCore.World.ReadPayloadFromDisk"/>'s
/// sharing mode only protects against a writer that isn't fully exclusive;
/// verified empirically (<c>Norn.Tests.WorldFileSharingTests</c>) that a
/// writer demanding full exclusivity (<c>FileShare.None</c>) is blocked by
/// Norn's read regardless of how permissive Norn's own share flags are —
/// an earlier pass here claimed the sharing mode alone made this
/// impossible "regardless of timing," which was wrong. This gate is the
/// actual (best-effort, not guaranteed) protection for that case.
/// <see cref="WorldDirectoryResolver"/>'s local folder is never gated this
/// way — nothing here suggests Valheim itself contends over that folder.
/// </para>
/// </summary>
public static class WorldIdentityShim
{
    private const string CacheFileName = "worlds.csv";

    /// <summary>Runs the scan and refreshes the catalog. Returns the number of
    /// (non-backup) <c>.fwl</c> files found across every source, for
    /// status-bar reporting.</summary>
    public static int RefreshCatalog()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var directories = new List<string> { WorldDirectoryResolver.ResolveLocalWorldDirectory(platform, home) };
        if (!GameProcessShim.IsGameRunning())
        {
            directories.AddRange(SteamCloudWorldShim.FindCloudWorldDirectories());
        }

        var worldFiles = WorldFileLocator.FindWorldFiles(directories);

        var cachePath = Path.Combine(
            AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home),
            CacheFileName);

        WorldIdentityCatalog.Refresh(cachePath, worldFiles);

        return worldFiles.Count;
    }
}
