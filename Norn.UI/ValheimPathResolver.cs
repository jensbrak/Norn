namespace Norn.UI;

/// <summary>
/// Shared path-joining logic for Valheim's per-platform save-data layout.
/// <see cref="SaveDirectoryResolver"/> and <see cref="WorldDirectoryResolver"/>
/// each carried a byte-for-byte identical private copy of this (found in
/// review) — they differ only in the leaf folder name they pass in.
/// </summary>
internal static class ValheimPathResolver
{
    /// <summary>
    /// Joined with the <em>target</em> platform's own separator — deliberately
    /// not <see cref="Path.Combine"/>, which joins with the host's separator and
    /// would turn a Linux path into nonsense when composed on Windows, defeating
    /// the reason this function takes <paramref name="platform"/> as data.
    /// </summary>
    public static string Resolve(PlatformKind platform, string homeDirectory, string leaf)
    {
        return platform switch
        {
            PlatformKind.Windows => Join('\\', homeDirectory, "AppData", "LocalLow", "IronGate", "Valheim", leaf),
            PlatformKind.Linux => Join('/', homeDirectory, ".config", "unity3d", "IronGate", "Valheim", leaf),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };
    }

    private static string Join(char separator, string home, params string[] segments)
        => home.TrimEnd(separator) + separator + string.Join(separator, segments);
}
