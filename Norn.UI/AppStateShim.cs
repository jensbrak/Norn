namespace Norn.UI;

/// <summary>
/// The platform-gated glue for <see cref="AppState"/> persistence:
/// resolves the per-user <c>state.json</c> path and loads
/// <see cref="AppStateStore"/>. Called once at startup, before any window
/// is shown, from <see cref="Program"/> — same timing as
/// <see cref="SettingsShim"/>/<see cref="ItemCatalogShim"/>.
/// </summary>
public static class AppStateShim
{
    private const string FileName = "state.json";

    public static void Load()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var path = Path.Combine(AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home), FileName);
        AppStateStore.Load(path);
    }
}
