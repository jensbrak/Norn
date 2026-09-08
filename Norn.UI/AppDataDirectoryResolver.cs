namespace Norn.UI;

/// <summary>
/// Where Norn stores its own per-user data — today just an optional override
/// copy of the shared item-data CSV. A pure function over
/// <c>(platform, homeDirectory)</c> — no environment reads, no filesystem
/// access — same shape and reasoning as <see cref="SaveDirectoryResolver"/>
/// Deliberately does not read <c>$XDG_DATA_HOME</c> on
/// Linux, hardcoding its default (<c>~/.local/share</c>) instead — the same
/// simplification <see cref="SaveDirectoryResolver"/> already makes for
/// Valheim's own directory, and full XDG env-var compliance isn't needed
/// until something actually requires it.
/// </summary>
public static class AppDataDirectoryResolver
{
    public static string ResolveAppDataDirectory(PlatformKind platform, string homeDirectory)
    {
        return platform switch
        {
            PlatformKind.Windows => Join('\\', homeDirectory, "AppData", "Local", "Norn"),
            PlatformKind.Linux => Join('/', homeDirectory, ".local", "share", "Norn"),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };
    }

    private static string Join(char separator, string home, params string[] segments)
        => home.TrimEnd(separator) + separator + string.Join(separator, segments);
}
