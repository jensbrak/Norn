using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// <see cref="WelcomePolicy.ShouldShow"/> is a pure function specifically so
/// this can exercise it directly, no app/settings/file I/O involved.
/// </summary>
public class WelcomePolicyTests
{
    [Fact]
    public void No_stored_version_shows_including_true_first_run()
    {
        Assert.True(WelcomePolicy.ShouldShow(null, "0.1.0", enabled: true));
    }

    [Fact]
    public void Same_version_does_not_show()
    {
        Assert.False(WelcomePolicy.ShouldShow("0.1.0", "0.1.0", enabled: true));
    }

    [Fact]
    public void Patch_only_bump_does_not_show()
    {
        Assert.False(WelcomePolicy.ShouldShow("0.1.0", "0.1.1", enabled: true));
    }

    [Fact]
    public void Minor_bump_shows()
    {
        Assert.True(WelcomePolicy.ShouldShow("0.1.0", "0.2.0", enabled: true));
    }

    [Fact]
    public void Major_bump_shows()
    {
        Assert.True(WelcomePolicy.ShouldShow("0.9.0", "1.0.0", enabled: true));
    }

    [Fact]
    public void Major_bump_shows_even_if_minor_drops()
    {
        Assert.True(WelcomePolicy.ShouldShow("1.5.0", "2.0.0", enabled: true));
    }

    [Fact]
    public void Older_stored_version_does_not_show_backwards()
    {
        Assert.False(WelcomePolicy.ShouldShow("0.2.0", "0.1.0", enabled: true));
    }

    [Fact]
    public void Disabled_never_shows_even_on_first_run()
    {
        Assert.False(WelcomePolicy.ShouldShow(null, "0.1.0", enabled: false));
    }

    [Fact]
    public void Disabled_never_shows_even_on_a_major_bump()
    {
        Assert.False(WelcomePolicy.ShouldShow("0.9.0", "1.0.0", enabled: false));
    }

    [Fact]
    public void Unparseable_stored_version_fails_closed()
    {
        Assert.False(WelcomePolicy.ShouldShow("not-a-version", "0.1.0", enabled: true));
    }

    [Fact]
    public void Unparseable_current_version_fails_closed()
    {
        Assert.False(WelcomePolicy.ShouldShow("0.1.0", "not-a-version", enabled: true));
    }
}
