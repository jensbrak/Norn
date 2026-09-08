using Norn.UI;

namespace Norn.Tests;

/// <summary>Exercises <see cref="SteamPathResolver"/>'s pure per-platform
/// rules — same shape as <see cref="SaveDirectoryResolverTests"/>.</summary>
public class SteamPathResolverTests
{
    [Fact]
    public void Windows_uses_the_registry_value_when_present()
    {
        var actual = SteamPathResolver.ResolveSteamInstallPath(
            PlatformKind.Windows, @"D:\Program Files (x86)\Steam", @"C:\Users\test-user");

        Assert.Equal(@"D:\Program Files (x86)\Steam", actual);
    }

    [Fact]
    public void Windows_returns_null_when_the_registry_value_is_absent()
    {
        var actual = SteamPathResolver.ResolveSteamInstallPath(
            PlatformKind.Windows, null, @"C:\Users\test-user");

        Assert.Null(actual);
    }

    [Fact]
    public void Linux_uses_a_hardcoded_default_ignoring_any_registry_value()
    {
        var actual = SteamPathResolver.ResolveSteamInstallPath(
            PlatformKind.Linux, "should be ignored", "/home/test-user");

        Assert.Equal("/home/test-user/.local/share/Steam", actual);
    }
}
