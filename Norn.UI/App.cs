using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Norn.UI;

/// <summary>
/// Equivalent of the template's <c>App.axaml</c> + <c>App.axaml.cs</c>, written
/// in C# — <c>.axaml</c> is banned.
/// </summary>
public sealed class App : Application
{
    /// <summary>Set by <see cref="Program.Main"/> before the framework starts,
    /// since Avalonia constructs <see cref="App"/> itself — no clean
    /// constructor-injection point exists for one CLI value.</summary>
    internal static string? InitialFilePath { get; set; }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // SettingsStore.Load already ran in Program.Main, before this
        // constructor-equivalent — same "load before any window exists"
        // ordering ItemCatalogShim/LocalizationCatalogShim already rely on.
        RequestedThemeVariant = SettingsStore.Current.Theme.ToThemeVariant();

        // Fluent's default horizontal Slider reserves 15px above and below
        // the track unconditionally (tick-mark/label space nothing in Norn
        // ever uses) plus a 32px MinHeight, making every slider ~50px tall
        // against a 20px thumb — the "inflated, sitting low" feel that no
        // per-row alignment tweak could fix. Redefining
        // these theme resource keys is the Avalonia-maintainer-sanctioned
        // lightweight customization path (as opposed to a full
        // ControlTemplate replacement). One app-wide fix,
        // not a per-instance Height (which can't shrink below the fixed
        // pre/post rows without cropping the thumb).
        Resources["SliderPreContentMargin"] = new GridLength(4);
        Resources["SliderPostContentMargin"] = new GridLength(4);
        Resources["SliderHorizontalHeight"] = 24.0;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Dispatcher.UIThread only exists once Avalonia's platform threading
        // is set up, which is why this isn't in CrashReporter.Install()
        // alongside the AppDomain/TaskScheduler hooks — but it must still
        // run before MainWindow's constructor wires up its own async void
        // event handlers, so a failure in any of those is covered too.
        CrashReporter.InstallDispatcherHandler();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(InitialFilePath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
