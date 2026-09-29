using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Platform-gated loading for <see cref="PieceCatalog"/> — same shape as
/// <see cref="RecipeCatalogShim"/>. Called once at startup from
/// <see cref="Program"/>.
/// </summary>
public static class PieceCatalogShim
{
    private const string FileName = "PieceData.json";

    public static void LoadCatalog()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var externalPath = Path.Combine(
            AppDataDirectoryResolver.ResolveAppDataDirectory(platform, home),
            FileName);
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Content", FileName);

        PieceCatalog.Load(externalPath, bundledPath);
    }
}
