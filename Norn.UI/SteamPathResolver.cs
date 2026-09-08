namespace Norn.UI;

/// <summary>
/// Where Steam's own install directory lives, given what the environment
/// shim already found — pure function over its inputs, no
/// registry access, no filesystem access here; that's <see cref="SteamCloudWorldShim"/>'s
/// job. Unlike <see cref="SaveDirectoryResolver"/>/<see cref="WorldDirectoryResolver"/>,
/// this location genuinely varies per machine (there is no fixed
/// home-directory-relative path Steam always installs to — one real
/// install is <c>D:\Program Files (x86)\Steam</c>, not the default
/// <c>C:\</c>), so the Windows branch depends on a value the shim reads
/// from the registry rather than being derivable from platform + home
/// directory alone.
/// </summary>
public static class SteamPathResolver
{
    /// <summary>
    /// Windows: <paramref name="registrySteamPath"/> (read by the shim from
    /// <c>HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath</c>, the key
    /// Steam itself writes) if present; <c>null</c> when Steam isn't
    /// installed or the key is absent — a handled case, not an error.
    /// Linux: a hardcoded default (<c>~/.local/share/Steam</c>), same
    /// "don't over-engineer it" precedent <see cref="AppDataDirectoryResolver"/>
    /// already sets for not resolving <c>$XDG_DATA_HOME</c> — <b>unverified
    /// against a real Linux Steam install as of this pass</b>
    /// — best-effort until confirmed.
    /// </summary>
    public static string? ResolveSteamInstallPath(PlatformKind platform, string? registrySteamPath, string homeDirectory)
    {
        return platform switch
        {
            PlatformKind.Windows => registrySteamPath,
            PlatformKind.Linux => homeDirectory.TrimEnd('/') + "/.local/share/Steam",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };
    }
}
