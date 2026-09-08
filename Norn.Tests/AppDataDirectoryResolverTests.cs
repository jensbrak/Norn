using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="AppDataDirectoryResolver"/>'s pure per-platform
/// rules — same shape as <see cref="SaveDirectoryResolverTests"/>, both
/// branches asserted regardless of which platform hosts the test run.
/// </summary>
public class AppDataDirectoryResolverTests
{
    [Fact]
    public void Resolves_the_windows_app_data_directory()
    {
        var actual = AppDataDirectoryResolver.ResolveAppDataDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.Equal(@"C:\Users\test-user\AppData\Local\Norn", actual);
    }

    [Fact]
    public void Resolves_the_linux_app_data_directory()
    {
        var actual = AppDataDirectoryResolver.ResolveAppDataDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.Equal("/home/test-user/.local/share/Norn", actual);
    }

    [Fact]
    public void Linux_resolution_uses_forward_slashes_even_on_a_windows_host()
    {
        var actual = AppDataDirectoryResolver.ResolveAppDataDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.DoesNotContain('\\', actual);
    }

    [Fact]
    public void Windows_resolution_uses_backslashes_even_on_a_linux_host()
    {
        var actual = AppDataDirectoryResolver.ResolveAppDataDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.DoesNotContain('/', actual);
    }
}
