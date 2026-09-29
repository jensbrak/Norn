using Avalonia;

namespace Norn.UI;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things
    // aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // First, before anything else can throw: pure .NET, no Avalonia
        // dependency, so it also catches a crash inside the startup shims
        // below. See CrashReporter's own doc comment for the full story.
        CrashReporter.Install();

        // "Open with" support: a .fch path on the command line loads that one
        // file directly instead of scanning the default save directory.
        App.InitialFilePath = args.FirstOrDefault(a =>
            a.EndsWith(".fch", StringComparison.OrdinalIgnoreCase) && File.Exists(a));

        // Independent loads, none depending on another's output — the
        // catalog shims each parse a large bundled file via blocking I/O, so
        // running them serially would sum their latency into the
        // empty-window gap on every launch.
        Parallel.Invoke(
            ItemCatalogShim.LoadCatalog,
            LocalizationCatalogShim.LoadCatalog,
            RecipeCatalogShim.LoadCatalog,
            PieceCatalogShim.LoadCatalog,
            SettingsShim.LoadSettings,
            AppStateShim.Load,
            HelpShim.Load,
            ReleaseNotesShim.Load);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
