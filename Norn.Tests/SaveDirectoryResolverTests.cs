using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="SaveDirectoryResolver"/>'s pure per-platform rules. Both
/// branches are asserted regardless of which platform hosts the test run — that
/// is the entire point of the function taking <see cref="PlatformKind"/> as
/// data rather than reading it from the environment.
/// </summary>
public class SaveDirectoryResolverTests
{
    [Fact]
    public void Resolves_the_windows_save_directory()
    {
        var actual = SaveDirectoryResolver.ResolveSaveDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.Equal(@"C:\Users\test-user\AppData\LocalLow\IronGate\Valheim\characters", actual);
    }

    [Fact]
    public void Resolves_the_windows_local_save_directory()
    {
        var actual = SaveDirectoryResolver.ResolveLocalSaveDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.Equal(@"C:\Users\test-user\AppData\LocalLow\IronGate\Valheim\characters_local", actual);
    }

    [Fact]
    public void Resolves_the_linux_save_directory()
    {
        var actual = SaveDirectoryResolver.ResolveSaveDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.Equal("/home/test-user/.config/unity3d/IronGate/Valheim/characters", actual);
    }

    [Fact]
    public void Resolves_the_linux_local_save_directory()
    {
        var actual = SaveDirectoryResolver.ResolveLocalSaveDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.Equal("/home/test-user/.config/unity3d/IronGate/Valheim/characters_local", actual);
    }

    [Fact]
    public void Linux_resolution_uses_forward_slashes_even_on_a_windows_host()
    {
        var actual = SaveDirectoryResolver.ResolveSaveDirectory(PlatformKind.Linux, "/home/test-user");

        Assert.DoesNotContain('\\', actual);
    }

    [Fact]
    public void Windows_resolution_uses_backslashes_even_on_a_linux_host()
    {
        var actual = SaveDirectoryResolver.ResolveSaveDirectory(PlatformKind.Windows, @"C:\Users\test-user");

        Assert.DoesNotContain('/', actual);
    }
}

/// <summary>Exercises <see cref="SaveFileLocator"/>'s case-insensitive matching.</summary>
public class SaveFileLocatorTests
{
    [Fact]
    public void Finds_a_save_file_with_an_uppercase_extension()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "character.FCH");
        File.WriteAllBytes(path, []);

        var found = SaveFileLocator.FindSaveFiles([directory.Path]);

        Assert.Contains(path, found);
    }

    [Fact]
    public void Ignores_files_with_a_different_extension()
    {
        using var directory = TempDirectory.Create();
        File.WriteAllBytes(Path.Combine(directory.Path, "notes.txt"), []);

        var found = SaveFileLocator.FindSaveFiles([directory.Path]);

        Assert.Empty(found);
    }

    [Fact]
    public void Skips_a_directory_that_does_not_exist()
    {
        var missing = TempFile.NonExistentPath();

        var found = SaveFileLocator.FindSaveFiles([missing]);

        Assert.Empty(found);
    }
}

/// <summary>
/// The one test that touches the real filesystem via <see cref="SaveDirectoryShim"/>.
/// Skips cleanly when the directory is absent — the host running this
/// may or may not have Valheim installed.
/// </summary>
public class SaveDirectoryShimTests
{
    [Fact]
    public void Real_save_directories_contain_only_fch_files_when_present()
    {
        var directories = SaveDirectoryShim.ResolveSaveDirectories();
        var existing = directories.Where(Directory.Exists).ToList();

        Assert.SkipWhen(existing.Count == 0, "No Valheim save directory present on this host.");

        var found = SaveFileLocator.FindSaveFiles(existing);

        Assert.All(found, path => Assert.Equal(".fch", Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
    }
}
