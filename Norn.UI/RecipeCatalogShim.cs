using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Platform-gated loading for <see cref="RecipeCatalog"/> — same shape as
/// <see cref="ItemCatalogShim"/>: external per-user override first, bundled
/// default second, empty if neither exists. Called once at startup from
/// <see cref="Program"/>.
/// </summary>
public static class RecipeCatalogShim
{
    private const string FileName = "RecipeData.json";

    public static void LoadCatalog()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var externalPath = Path.Combine(
            AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home),
            FileName);
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Content", FileName);

        RecipeCatalog.Load(externalPath, bundledPath);
    }
}
