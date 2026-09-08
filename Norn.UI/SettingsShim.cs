namespace Norn.UI;

/// <summary>
/// The platform-gated glue for settings persistence:
/// resolves the per-user <c>settings.json</c> path and loads
/// <see cref="SettingsStore"/>. Called once at startup, before any window is
/// shown, from <see cref="Program"/> — same timing as
/// <see cref="ItemCatalogShim"/>/<see cref="LocalizationCatalogShim"/> (a
/// small file, fast enough to read before the window exists).
/// </summary>
public static class SettingsShim
{
    private const string FileName = "settings.json";

    public static void LoadSettings()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var path = Path.Combine(AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home), FileName);
        SettingsStore.Load(path);
    }
}
