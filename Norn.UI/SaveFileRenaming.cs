namespace Norn.UI;

/// <summary>
/// Validity/availability checks for renaming a save file's stem to match an
/// edited player name. Deliberately platform-independent rather than
/// host-gated: both checks apply the stricter of Windows'/Linux's rules
/// regardless of which platform Norn is running on, so a name that passes
/// is guaranteed valid wherever the resulting file might later be opened —
/// same reasoning as <see cref="SaveFileLocator"/>'s case-insensitive
/// extension match.
/// </summary>
public static class SaveFileRenaming
{
    // Windows forbids these plus control chars 0-31; Linux only actually
    // forbids '/' and NUL. The superset costs nothing here and buys a name
    // that's safe on both.
    private static readonly char[] IllegalCharacters = "<>:\"/\\|?*".ToCharArray();

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// <c>null</c> when <paramref name="candidateStem"/> (the filename
    /// without extension) is valid on both platforms; otherwise a
    /// user-facing reason it isn't.
    /// </summary>
    public static string? ValidationError(string candidateStem)
    {
        if (string.IsNullOrWhiteSpace(candidateStem))
        {
            return "The name can't be empty.";
        }

        if (candidateStem.IndexOfAny(IllegalCharacters) >= 0 || candidateStem.Any(c => c < 32))
        {
            return "The name contains characters that aren't allowed in a filename.";
        }

        if (candidateStem.EndsWith('.') || candidateStem.EndsWith(' '))
        {
            return "The name can't end with a period or a space.";
        }

        var dot = candidateStem.IndexOf('.');
        var baseName = dot < 0 ? candidateStem : candidateStem[..dot];
        if (ReservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
        {
            return $"\"{baseName}\" is a reserved filename on Windows.";
        }

        return null;
    }

    /// <summary>
    /// True if no <i>other</i> file in <paramref name="directory"/> already
    /// has <paramref name="candidateStem"/> + <paramref name="extension"/> as
    /// its name, checked case-insensitively regardless of host — a collision
    /// that would silently overwrite on Windows must also be caught when
    /// Norn happens to be running on Linux. <paramref name="excludeFileName"/>
    /// is the file actually being renamed, if any — without it, a
    /// case-only rename (e.g. "Erik" to "erik") always reported a false
    /// collision, since the not-yet-renamed original still matched under
    /// the case-insensitive comparison this method deliberately uses (found
    /// in review). Optional, defaulted to preserve existing callers/tests
    /// that were never checking a rename-in-place scenario to begin with.
    /// </summary>
    public static bool IsNameAvailable(string directory, string candidateStem, string extension, string? excludeFileName = null)
    {
        if (!Directory.Exists(directory))
        {
            return true;
        }

        var candidateFileName = candidateStem + extension;
        return !Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .Any(name => !string.Equals(name, excludeFileName, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(name, candidateFileName, StringComparison.OrdinalIgnoreCase));
    }
}
