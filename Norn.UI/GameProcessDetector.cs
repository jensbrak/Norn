namespace Norn.UI;

/// <summary>
/// Pure per-platform rule for whether Valheim itself appears to be running,
/// mirroring <see cref="SaveDirectoryResolver"/>'s shape: platform and the
/// observed process list are both arguments, not read from the environment,
/// so both branches are testable from either host.
/// </summary>
/// <remarks>
/// Advisory only — never a hard block (see <see cref="GameProcessShim"/> and
/// <c>MainWindow</c>'s banner wiring). <b>Known reliability gap, not solved
/// here:</b> a Windows build of Valheim run under Proton on Linux does not
/// reliably surface a process named <c>valheim</c> or <c>valheim.x86_64</c>
/// — Proton/Wine's process tree varies by version and launch method, so
/// detection is weaker on Linux than on Windows. Documented rather than
/// worked around, since a false negative here is no worse than having no
/// check at all, and the check must never be relied on as a guarantee.
/// </remarks>
public static class GameProcessDetector
{
    private static readonly IReadOnlyDictionary<PlatformKind, IReadOnlyList<string>> KnownProcessNames = new Dictionary<PlatformKind, IReadOnlyList<string>>
    {
        [PlatformKind.Windows] = ["valheim"],
        [PlatformKind.Linux] = ["valheim.x86_64", "valheim"],
    };

    public static bool IsGameRunning(PlatformKind platform, IEnumerable<string> runningProcessNames)
    {
        var known = KnownProcessNames[platform];
        return runningProcessNames.Any(name => known.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
