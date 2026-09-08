using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Finds local world-metadata files across one or more directories, for
/// <see cref="WorldIdentityShim"/>'s identity scan — sibling to
/// <see cref="SaveFileLocator"/>, same case-insensitive-extension reasoning.
/// </summary>
/// <remarks>
/// <para>
/// Two layouts, both live. A legacy world is a flat <c>&lt;dir&gt;/Name.fwl</c>;
/// a Valheim 1.0 world is <c>&lt;dir&gt;/Name/_main.&lt;N&gt;.fwl2</c>. Both are
/// returned, because a real machine has both — 1.0 does not migrate old worlds
/// on sight, and Norn is read-only for world files either way.
/// </para>
/// <para>
/// Backup copies are excluded outright rather than surfaced behind a toggle:
/// there is no browsing UI for world files, so a backup would only ever add
/// scan noise or a spurious identity conflict for a world that never actually
/// changed. Filtering here, before any file is read, is cheaper than deduping
/// after the fact. Note the two layouts hide backups differently — a legacy
/// backup is a file name, a chunked backup is a whole directory — so the
/// filter has to be path-aware, not name-aware.
/// </para>
/// </remarks>
public static class WorldFileLocator
{
    private const string LegacyWorldFileExtension = ".fwl";

    public static IReadOnlyList<string> FindWorldFiles(IEnumerable<string> directories)
    {
        var files = new List<string>();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            files.AddRange(FindLegacyWorldFiles(directory));
            files.AddRange(FindChunkedWorldFiles(directory));
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> FindLegacyWorldFiles(string directory)
        => Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path)
                .Equals(LegacyWorldFileExtension, StringComparison.OrdinalIgnoreCase))
            .Where(path => !WorldFileClassifier.IsBackupFile(Path.GetFileName(path)));

    /// <remarks>
    /// One directory per world, and within it possibly several
    /// <c>_main.&lt;N&gt;.fwl2</c> files — the game increments N on every save
    /// and leaves the previous one behind, so the highest N is the current
    /// world and the rest are its predecessors. Taking all of them would report
    /// the same world two or three times, each a slightly older snapshot, which
    /// the identity catalog would then treat as a naming conflict.
    /// </remarks>
    private static IEnumerable<string> FindChunkedWorldFiles(string directory)
    {
        string[] worldDirectories;
        try
        {
            worldDirectories = Directory.GetDirectories(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var worldDirectory in worldDirectories)
        {
            // Per-directory, not around the whole loop. A real worlds folder
            // accumulates unrelated subdirectories over the years — the
            // author's own has a "bak" and a nested "worlds_local" — and one
            // of them being unreadable must cost that directory only, not
            // blank the entire Worlds tab. This is a user-chosen location full
            // of files Norn did not create, so it is the wrong place to assume
            // everything is enumerable.
            var current = TryFindCurrentSave(worldDirectory);
            if (current is not null)
            {
                yield return current;
            }
        }
    }

    private static string? TryFindCurrentSave(string worldDirectory)
    {
        try
        {
            return Directory.EnumerateFiles(worldDirectory)
                .Select(path => (Path: path, Number: WorldFileClassifier.TryGetChunkedSaveNumber(Path.GetFileName(path))))
                .Where(candidate => candidate.Number is not null)
                .Where(candidate => !WorldFileClassifier.IsBackupPath(candidate.Path))
                .OrderByDescending(candidate => candidate.Number!.Value)
                .Select(candidate => candidate.Path)
                .FirstOrDefault();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
