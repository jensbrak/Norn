using System.Diagnostics;

namespace Norn.UI;

/// <summary>
/// The thin environment-reading half of the game-running check — mirrors
/// <see cref="SaveDirectoryShim"/>'s split from <see cref="SaveDirectoryResolver"/>.
/// <see cref="Process.GetProcesses"/> is itself a cross-platform BCL API (it
/// reads <c>/proc</c> on Linux), so nothing here is platform-gated beyond
/// picking which name(s) count as a match, which <see cref="GameProcessDetector"/>
/// already handles as data.
/// </summary>
public static class GameProcessShim
{
    public static bool IsGameRunning()
    {
        var platform = PlatformDetection.Current;
        var processes = Process.GetProcesses();
        try
        {
            return GameProcessDetector.IsGameRunning(platform, processes.Select(p => p.ProcessName));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
