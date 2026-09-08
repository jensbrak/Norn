using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="SteamCloudWorldShim.FindCloudWorldDirectoriesUnder"/> —
/// the testable core, given a Steam install path directly rather than
/// reading the registry — against a synthetic <c>userdata</c> layout.
/// </summary>
public class SteamCloudWorldShimTests
{
    [Fact]
    public void Finds_a_worlds_folder_under_one_account()
    {
        using var steamRoot = TempDirectory.Create();
        var worldsDir = Path.Combine(steamRoot.Path, "userdata", "123456", "892970", "remote", "worlds");
        Directory.CreateDirectory(worldsDir);

        var found = SteamCloudWorldShim.FindCloudWorldDirectoriesUnder(steamRoot.Path);

        Assert.Equal([worldsDir], found);
    }

    [Fact]
    public void Finds_a_worlds_folder_under_every_account_present()
    {
        using var steamRoot = TempDirectory.Create();
        var worldsDirA = Path.Combine(steamRoot.Path, "userdata", "111", "892970", "remote", "worlds");
        var worldsDirB = Path.Combine(steamRoot.Path, "userdata", "222", "892970", "remote", "worlds");
        Directory.CreateDirectory(worldsDirA);
        Directory.CreateDirectory(worldsDirB);

        var found = SteamCloudWorldShim.FindCloudWorldDirectoriesUnder(steamRoot.Path);

        Assert.Equal(2, found.Count);
        Assert.Contains(worldsDirA, found);
        Assert.Contains(worldsDirB, found);
    }

    [Fact]
    public void Skips_an_account_with_no_valheim_remote_folder()
    {
        using var steamRoot = TempDirectory.Create();
        // An account that owns some other game, not Valheim.
        Directory.CreateDirectory(Path.Combine(steamRoot.Path, "userdata", "111", "730", "remote"));

        var found = SteamCloudWorldShim.FindCloudWorldDirectoriesUnder(steamRoot.Path);

        Assert.Empty(found);
    }

    [Fact]
    public void Returns_empty_when_there_is_no_userdata_folder_at_all()
    {
        using var steamRoot = TempDirectory.Create();

        var found = SteamCloudWorldShim.FindCloudWorldDirectoriesUnder(steamRoot.Path);

        Assert.Empty(found);
    }

    [Fact]
    public void Returns_empty_when_the_steam_path_itself_does_not_exist()
    {
        var missing = TempFile.NonExistentPath();

        var found = SteamCloudWorldShim.FindCloudWorldDirectoriesUnder(missing);

        Assert.Empty(found);
    }
}
