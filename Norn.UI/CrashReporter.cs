using Avalonia.Threading;

namespace Norn.UI;

/// <summary>
/// Norn's last line of defense against an uncaught exception silently
/// killing the process — the failure mode hit on Linux, 2026-09-02:
/// a permissions problem (most likely) writing <see cref="AppStateStore"/>'s
/// file crashed the app to desktop the instant the welcome window closed,
/// with nothing for the user — or for whoever is on the receiving end of a
/// bug report — to go on. Two tiers, because .NET's own unhandled-exception
/// surfaces aren't uniform:
/// <list type="bullet">
/// <item><see cref="Install"/> hooks <see cref="AppDomain.UnhandledException"/>
/// and <see cref="TaskScheduler.UnobservedTaskException"/> — fatal by the
/// time either fires (the CLR is already tearing the process down), so all
/// this tier can do is get the exception onto disk before that happens.
/// Called first thing in <see cref="Program.Main"/>, before any Avalonia API
/// exists, so it also covers a crash during the startup shims themselves.</item>
/// <item><see cref="InstallDispatcherHandler"/> hooks Avalonia's
/// <c>Dispatcher.UIThread.UnhandledException</c> — everything routed through
/// the UI dispatcher, which is most of Norn's own code, since an
/// <c>async void</c> event handler (e.g. <c>Opened += async (_, _) =>
/// ...</c> in <see cref="MainWindow"/>, the exact pattern behind the bug
/// this file guards against) throws here rather than through the AppDomain
/// surface. Unlike the AppDomain tier, this one is recoverable: marking the
/// event <c>Handled</c> lets the dispatch loop continue instead of
/// terminating, so a failure in one non-critical operation (an autosave, a
/// background scan) doesn't take the whole editor down with it.</item>
/// </list>
/// Both tiers write to the same log rather than attempt any UI of their
/// own — by the time either fires the app may be mid-teardown, or the
/// exception may itself be from UI code, so a message box is the wrong
/// tool here. The log file is the "something to go on" instead.
/// </summary>
internal static class CrashReporter
{
    private const string FileName = "crash.log";

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    public static void InstallDispatcherHandler()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log(e.Exception, "Dispatcher.UIThread.UnhandledException");
            e.Handled = true;
        };
    }

    private static void Log(Exception? exception, string source)
    {
        try
        {
            var platform = PlatformDetection.Current;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directory = AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home);
            Directory.CreateDirectory(directory);

            var entry =
                $"""
                 [{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {source}
                 Norn {AppInfo.Version} on {Environment.OSVersion}
                 {exception}

                 """;

            File.AppendAllText(Path.Combine(directory, FileName), entry);
        }
        catch
        {
            // Last-resort logging must never itself throw — there is
            // nowhere left to report a failure of the failure reporter.
        }
    }
}
