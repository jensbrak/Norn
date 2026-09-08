using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The only platform-gated code for item-catalog loading:
/// detects the running platform and home directory, resolves both candidate
/// CSV locations, and loads <see cref="SharedItemDataCatalog"/> — external
/// per-user override first, bundled default (shipped next to the executable)
/// second, empty if neither exists. Called once at startup,
/// before any window is shown, from <see cref="Program"/>.
/// </summary>
public static class ItemCatalogShim
{
    private const string FileName = "SharedItemData.csv";

    public static void LoadCatalog()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var externalPath = Path.Combine(
            AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home),
            FileName);
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Content", FileName);

        SharedItemDataCatalog.Load(externalPath, bundledPath);
    }
}
