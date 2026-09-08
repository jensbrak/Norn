using Norn.Adapter;
using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="WorldDirectoryResolver"/>'s pure per-platform rules —
/// same shape as <see cref="SaveDirectoryResolverTests"/>, both branches
/// asserted regardless of which platform hosts the test run.
/// </summary>
public class WorldDirectoryResolverTests
{
    [Fact]
    public void Resolves_the_windows_world_directory()
    {
        var actual = WorldDirectoryResolver.ResolveWorldDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.Equal(@"C:\Users\test-user\AppData\LocalLow\IronGate\Valheim\worlds", actual);
    }

    [Fact]
    public void Resolves_the_windows_local_world_directory()
    {
        var actual = WorldDirectoryResolver.ResolveLocalWorldDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.Equal(@"C:\Users\test-user\AppData\LocalLow\IronGate\Valheim\worlds_local", actual);
    }

    [Fact]
    public void Resolves_the_linux_world_directory()
    {
        var actual = WorldDirectoryResolver.ResolveWorldDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.Equal("/home/test-user/.config/unity3d/IronGate/Valheim/worlds", actual);
    }

    [Fact]
    public void Resolves_the_linux_local_world_directory()
    {
        var actual = WorldDirectoryResolver.ResolveLocalWorldDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.Equal("/home/test-user/.config/unity3d/IronGate/Valheim/worlds_local", actual);
    }

    [Fact]
    public void Linux_resolution_uses_forward_slashes_even_on_a_windows_host()
    {
        var actual = WorldDirectoryResolver.ResolveWorldDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.DoesNotContain('\\', actual);
    }

    [Fact]
    public void Windows_resolution_uses_backslashes_even_on_a_linux_host()
    {
        var actual = WorldDirectoryResolver.ResolveWorldDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.DoesNotContain('/', actual);
    }
}

/// <summary>Exercises <see cref="WorldFileLocator"/>'s case-insensitive
/// extension matching and backup exclusion.</summary>
public class WorldFileLocatorTests
{
    [Fact]
    public void Finds_a_world_file_with_an_uppercase_extension()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "MyWorld.FWL");
        File.WriteAllBytes(path, []);

        var found = WorldFileLocator.FindWorldFiles([directory.Path]);

        Assert.Contains(path, found);
    }

    [Fact]
    public void Ignores_files_with_a_different_extension()
    {
        using var directory = TempDirectory.Create();
        File.WriteAllBytes(Path.Combine(directory.Path, "MyWorld.db"), []);

        var found = WorldFileLocator.FindWorldFiles([directory.Path]);

        Assert.Empty(found);
    }

    [Fact]
    public void Skips_a_directory_that_does_not_exist()
    {
        var missing = TempFile.NonExistentPath();

        var found = WorldFileLocator.FindWorldFiles([missing]);

        Assert.Empty(found);
    }

    [Fact]
    public void Excludes_a_game_generated_backup_file()
    {
        using var directory = TempDirectory.Create();
        File.WriteAllBytes(Path.Combine(directory.Path, "MyWorld.fwl"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "MyWorld_backup_auto-20250104120000.fwl"), []);

        var found = WorldFileLocator.FindWorldFiles([directory.Path]);

        Assert.Single(found);
        Assert.EndsWith("MyWorld.fwl", found[0]);
    }
}

/// <summary>
/// The one test that touches the real filesystem via <see cref="WorldIdentityShim"/>'s
/// directory resolution. Skips cleanly when the directory is absent —
/// the host running this may or may not have Valheim
/// installed.
/// </summary>
public class WorldDirectoryShimTests
{
    // A review finding suggested calling WorldIdentityShim.RefreshCatalog()
    // directly instead of hand-deriving the platform here, to track that
    // method's own directory-selection logic. Not done as suggested: that
    // method has real side effects (it scans and writes a cache file to the
    // actual user's AppData directory) and also aggregates Steam-cloud
    // sources this test isn't scoped to cover — calling it would pollute
    // real state and broaden what "only recognized world files" would need
    // to mean. Applied the safe part instead: PlatformDetection.Current
    // (found in review, same dedup as every production shim) replaces the
    // test's own copy of the platform-detection ternary.
    [Fact]
    public void Real_local_world_directory_contains_only_recognized_world_files_when_present()
    {
        var platform = PlatformDetection.Current;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directory = WorldDirectoryResolver.ResolveLocalWorldDirectory(platform, home);

        Assert.SkipWhen(!Directory.Exists(directory), "No local Valheim world directory present on this host.");

        var found = WorldFileLocator.FindWorldFiles([directory]);

        // Both layouts are legitimate: a legacy world is a flat .fwl, a
        // Valheim 1.0 world is <Name>/_main.<N>.fwl2. This asserted ".fwl"
        // alone until 1.0.7, which is exactly how the locator's blindness to
        // the new layout stayed invisible — the assertion passed precisely
        // because nothing new was being found.
        Assert.All(found, path => Assert.True(
            WorldFileClassifier.IsWorldMetaFile(Path.GetFileName(path)),
            $"{path} is not a recognized world-metadata file."));

        Assert.All(found, path => Assert.False(
            WorldFileClassifier.IsBackupPath(path),
            $"{path} is a backup and should have been excluded."));
    }
}
