using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="GameProcessDetector"/>'s pure per-platform rule. Both
/// branches are asserted regardless of which platform hosts the test run,
/// same rationale as <see cref="SaveDirectoryResolverTests"/>.
/// </summary>
public class GameProcessDetectorTests
{
    [Fact]
    public void Detects_the_windows_process_name()
    {
        Assert.True(GameProcessDetector.IsGameRunning(PlatformKind.Windows, ["explorer", "valheim"]));
    }

    [Fact]
    public void Windows_match_is_case_insensitive()
    {
        Assert.True(GameProcessDetector.IsGameRunning(PlatformKind.Windows, ["Valheim"]));
    }

    [Fact]
    public void Detects_the_linux_native_process_name()
    {
        Assert.True(GameProcessDetector.IsGameRunning(PlatformKind.Linux, ["bash", "valheim.x86_64"]));
    }

    [Fact]
    public void Returns_false_when_no_known_name_is_present()
    {
        Assert.False(GameProcessDetector.IsGameRunning(PlatformKind.Windows, ["explorer", "chrome"]));
    }

    [Fact]
    public void Returns_false_for_an_empty_process_list()
    {
        Assert.False(GameProcessDetector.IsGameRunning(PlatformKind.Windows, []));
    }
}
