using Avalonia.Styling;

namespace Norn.UI;

/// <summary>
/// Norn's own theme vocabulary — kept distinct from Avalonia's own
/// <see cref="ThemeVariant"/> (a sealed class, not a plain enum, and awkward
/// to JSON-serialize) so <see cref="Settings"/>'s reflection/JSON contract
/// never has to know that type exists. <see cref="AppThemePreferenceExtensions.ToThemeVariant"/>
/// is the one place that translates between the two — applied at startup
/// (<see cref="App.Initialize"/>, after <see cref="SettingsStore.Load"/> has
/// already run) and again whenever <see cref="SettingsWindow"/> closes, so a
/// mid-session change takes effect immediately without a restart — Avalonia
/// re-themes every open window automatically once <c>RequestedThemeVariant</c>
/// is reassigned, no rebuild needed on Norn's side.
/// </summary>
public enum AppThemePreference
{
    System,
    Light,
    Dark,
}

public static class AppThemePreferenceExtensions
{
    public static ThemeVariant ToThemeVariant(this AppThemePreference preference) => preference switch
    {
        AppThemePreference.Light => ThemeVariant.Light,
        AppThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
