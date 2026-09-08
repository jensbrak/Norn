using System.Text.RegularExpressions;
using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// Everything Norn knows about how world saves are named and laid out on
/// disk — the only legal path from <c>Norn.UI</c> to that knowledge, mirroring
/// <see cref="SaveFileClassifier"/>'s role for character saves (<c>Norn.UI</c>
/// cannot reference <c>Norn.GameCore</c> directly).
/// </summary>
/// <remarks>
/// <para>
/// Valheim 1.0 gave world saves a second, completely different shape. Both are
/// still readable and both still exist on real machines, so every question
/// here has two answers:
/// </para>
/// <list type="bullet">
/// <item><description><b>Legacy (flat).</b> <c>&lt;root&gt;/worlds_local/MyWorld.fwl</c>,
/// with a sibling <c>.db</c>. Backups are files with a timestamp in the name.</description></item>
/// <item><description><b>Chunked (1.0+).</b> A directory per world,
/// <c>&lt;root&gt;/worlds_local/MyWorld/_main.2.fwl2</c>, alongside
/// <c>.db2</c>, <c>.chunks</c>, <c>.ok</c> and <c>*.chunk</c>. The world's
/// name is the <i>directory</i> name, not the file's. Backups are whole
/// directories with a timestamp in the name.</description></item>
/// </list>
/// <para>
/// This is not wire-format knowledge, so it carries no mirror-discipline
/// obligation — but it is game-derived and it is exactly the kind of thing a
/// patch can change silently, because getting it wrong produces no parse error
/// at all. It produces an empty list, which looks identical to "this user has
/// no worlds".
/// </para>
/// </remarks>
public static class WorldFileClassifier
{
    // game-derived: SaveSystem's file-ending constants — c_OldFwlFileEnding
    // (".fwl") and c_WorldFwl2FileEnding (".fwl2"). Confirmed against 1.0.7.
    private const string LegacyWorldExtension = ".fwl";
    private const string ChunkedWorldExtension = ".fwl2";

    // game-derived: SaveSystem's c_MainFileName. Every file in a chunked
    // world's directory except the per-chunk data is "_main.<saveNumber>.<ext>".
    // Confirmed against 1.0.7.
    private const string ChunkedMainPrefix = "_main.";

    // game-derived: SaveSystem.IsChunkedSave / IsWorldBackup both test the
    // grandparent directory name against exactly these two literals.
    // Confirmed against 1.0.7.
    private static readonly string[] WorldRootDirectoryNames = ["worlds", "worlds_local"];

    // game-derived: the timestamp pattern SaveSystem.IsWorldBackup matches
    // against a backup directory's name. Reproduced as source writes it,
    // including the optional separators. Confirmed against 1.0.7.
    private static readonly Regex BackupTimestampPattern = new(
        @".+(\d{4})-*(\d{2})-*(\d{2})-*(\d{2})-*(\d{2})-*(\d{2})",
        RegexOptions.Compiled);

    // game-derived: SaveSystem.MainFwl2FileName composes "_main." + number +
    // ".fwl2", and BeginSave sets the number to the previous one plus one — so
    // when several are present the HIGHEST is the newest. Confirmed against
    // 1.0.7.
    private static readonly Regex ChunkedMainFwl2Pattern = new(
        @"^_main\.(\d+)\.fwl2$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// True for a legacy flat <c>.fwl</c> whose name matches one of the game's
    /// own backup shapes. Legacy files only — see <see cref="IsBackupPath"/>
    /// for the question that covers both layouts.
    /// </summary>
    public static bool IsBackupFile(string fileName)
        => SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName, LegacyWorldExtension);

    /// <summary>
    /// True when <paramref name="fileName"/> is a chunked (1.0+) world's
    /// metadata file — i.e. <c>_main.&lt;N&gt;.fwl2</c>.
    /// </summary>
    /// <remarks>
    /// <b>This, not <see cref="IsChunkedWorldPath"/>, is what Norn's own
    /// discovery uses.</b> The game's test additionally requires the
    /// grandparent directory to be literally <c>worlds</c> or
    /// <c>worlds_local</c>, because it is asked about arbitrary paths
    /// including <c>.chunk</c> data files and needs to tell those apart from
    /// unrelated files. Norn only ever asks the narrower question "is this
    /// world-metadata file the chunked kind?", and the file's own name answers
    /// that unambiguously — a file called <c>_main.2.fwl2</c> is not anything
    /// else.
    /// <para>
    /// Keying off the name rather than the ancestry is also the more robust
    /// choice for a tool that reads files the user points it at: a world
    /// directory copied to a backup drive, a test corpus, or anywhere else
    /// outside a <c>worlds</c> folder is still a chunked world, and the game's
    /// own test would say it is not.
    /// </para>
    /// </remarks>
    public static bool IsChunkedWorldFile(string fileName)
        => TryGetChunkedSaveNumber(fileName) is not null;

    /// <summary>
    /// The game's own chunked-save test, reproduced exactly: grandparent
    /// directory named <c>worlds</c> or <c>worlds_local</c>, and a file that is
    /// either a <c>_main.*</c> or a <c>*.chunk</c>.
    /// </summary>
    /// <remarks>
    /// Kept because it is the answer to "would Valheim treat this path as a
    /// chunked save", which is a different and occasionally useful question
    /// from the one <see cref="IsChunkedWorldFile"/> answers. Norn's discovery
    /// deliberately does not use it — see the remarks there.
    /// </remarks>
    // game-derived: SaveSystem.IsChunkedSave. Confirmed against 1.0.7.
    public static bool IsChunkedWorldPath(string path)
    {
        var grandparent = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
        if (grandparent is null || !WorldRootDirectoryNames.Contains(grandparent, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return fileName.StartsWith(ChunkedMainPrefix, StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".chunk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when <paramref name="path"/> is a backup rather than a live world,
    /// in either layout.
    /// </summary>
    /// <remarks>
    /// For a chunked world the timestamp is on the <i>directory</i>, so this
    /// cannot be answered from the file name alone — which is why the
    /// file-name-only <see cref="IsBackupFile"/> is not enough on its own any
    /// more.
    /// </remarks>
    // game-derived: SaveSystem.IsWorldBackup. Confirmed against 1.0.7.
    public static bool IsBackupPath(string path)
    {
        var directory = Path.GetDirectoryName(path);

        // Chunked: the timestamp is on the containing directory, and the file
        // name is what identifies the layout — same reasoning as
        // IsChunkedWorldFile, so that a world directory outside a `worlds`
        // folder still has its backups recognised.
        if (IsChunkedWorldFile(Path.GetFileName(path)))
        {
            return directory is not null && BackupTimestampPattern.IsMatch(Path.GetFileName(directory) ?? "");
        }

        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".fwl.old", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".db.old", StringComparison.OrdinalIgnoreCase)
            || ((fileName.EndsWith(LegacyWorldExtension, StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                && BackupTimestampPattern.IsMatch(fileName));
    }

    /// <summary>
    /// The world's own save name for <paramref name="path"/> — the directory
    /// name for a chunked world, the file stem for a legacy one.
    /// </summary>
    /// <remarks>
    /// Not the world's in-file name (that comes from the payload); this is the
    /// name the save is filed under, which is what a user recognises and what
    /// the identity cache keys its "seen as" list on. Taking the stem for a
    /// chunked world would yield <c>_main.2</c> for every world on the machine.
    /// </remarks>
    // game-derived: SaveSystem.GetChunkedSaveName. Confirmed against 1.0.7.
    public static string GetWorldSaveName(string path)
        => IsChunkedWorldFile(Path.GetFileName(path))
            ? Path.GetFileName(Path.GetDirectoryName(path)) ?? ""
            : Path.GetFileNameWithoutExtension(path);

    /// <summary>
    /// The save number in a chunked world's <c>_main.&lt;N&gt;.fwl2</c>, or
    /// <c>null</c> when <paramref name="fileName"/> is not one.
    /// </summary>
    public static int? TryGetChunkedSaveNumber(string fileName)
    {
        var match = ChunkedMainFwl2Pattern.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : null;
    }

    /// <summary>True for a world-metadata file of either layout.</summary>
    public static bool IsWorldMetaFile(string fileName)
        => Path.GetExtension(fileName).Equals(LegacyWorldExtension, StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(fileName).Equals(ChunkedWorldExtension, StringComparison.OrdinalIgnoreCase);
}
