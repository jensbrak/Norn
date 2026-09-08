using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Platform-gated loading for <see cref="LocalizationCatalog"/> — same
/// shape as <see cref="ItemCatalogShim"/> (external per-user override
/// first, bundled default second, empty if neither exists), a separate
/// file rather than folded into <see cref="ItemCatalogShim"/> since the two
/// catalogs are independent seams onto independent CSVs (the
/// "concentrated behind one seam" principle applies per catalog, not to
/// "catalog loading" as one lumped concern). Called once at startup, before
/// any window is shown, from <see cref="Program"/>.
/// </summary>
public static class LocalizationCatalogShim
{
    private const string FileName = "LocalizationData.csv";

    public static void LoadCatalog()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var externalPath = Path.Combine(
            AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home),
            FileName);
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Content", FileName);

        LocalizationCatalog.Load(externalPath, bundledPath);
    }
}
