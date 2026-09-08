using Norn.Adapter;
using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="WorldIdentityCatalog"/>'s merge policy (first-seen
/// wins, conflict recording) and persistence, against synthetic <c>.fwl</c>
/// files — deterministic, independent of the real world corpus. In the shared
/// <see cref="CatalogCollection"/>: <see cref="WorldIdentityCatalog"/> is a
/// shared static exactly like <see cref="SharedItemDataCatalog"/>/
/// <see cref="LocalizationCatalog"/>, same race risk under xUnit's default
/// cross-collection parallelism.
/// </summary>
[Collection(CatalogCollection.Name)]
public class WorldIdentityCatalogTests
{
    private const long UidA = 1111111111L;
    private const long UidB = 2222222222L;

    // Version 9 (the oldest compatible) deliberately, not 37: at 9, name/
    // seedName/seed/uid are the entire payload — no worldGenVersion/needsDB/
    // startingGlobalKeys fields to also write, unlike a real version-37 file
    // (see WorldTests, which covers those gates directly). Writing "version
    // 37" with only 5 fields here would silently fail to parse and defeat
    // every test in this class.
    private static void WriteWorldFile(
        string path, string name, string seedName, int seed, long uid)
    {
        var payload = new ZPackage();
        payload.Write(9);
        payload.Write(name);
        payload.Write(seedName);
        payload.Write(seed);
        payload.Write(uid);

        using var fileStream = File.Create(path);
        using var binary = new BinaryWriter(fileStream);
        var bytes = payload.GetArray();
        binary.Write(bytes.Length);
        binary.Write(bytes);
    }

    [Fact]
    public void Refresh_creates_an_entry_for_each_valid_world_file_with_no_existing_cache()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var fileA = Path.Combine(directory.Path, "a.fwl");
        var fileB = Path.Combine(directory.Path, "b.fwl");
        WriteWorldFile(fileA, "Hearthhold", "8f3ha2", 123, UidA);
        WriteWorldFile(fileB, "Frostvale", "q7z9x1", 456, UidB);

        WorldIdentityCatalog.Refresh(cachePath, [fileA, fileB]);

        var a = WorldIdentityCatalog.TryFind(UidA);
        var b = WorldIdentityCatalog.TryFind(UidB);
        Assert.NotNull(a);
        Assert.Equal("Hearthhold", a!.Name);
        Assert.Equal("8f3ha2", a.SeedName);
        Assert.Equal(123, a.Seed);
        Assert.Equal(["a"], a.SeenAsFiles);
        Assert.Null(a.ConflictNote);
        Assert.NotNull(b);
        Assert.Equal("Frostvale", b!.Name);
        Assert.True(File.Exists(cachePath));
    }

    [Fact]
    public void Refresh_skips_an_unreadable_world_file_without_throwing()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var goodFile = Path.Combine(directory.Path, "good.fwl");
        var badFile = Path.Combine(directory.Path, "bad.fwl");
        WriteWorldFile(goodFile, "Hearthhold", "8f3ha2", 123, UidA);
        File.WriteAllBytes(badFile, [1, 2, 3]);

        var exception = Record.Exception(() => WorldIdentityCatalog.Refresh(cachePath, [goodFile, badFile]));

        Assert.Null(exception);
        Assert.NotNull(WorldIdentityCatalog.TryFind(UidA));
    }

    [Fact]
    public void Refresh_keeps_the_first_seen_name_and_records_a_conflict_on_a_later_disagreement()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var original = Path.Combine(directory.Path, "original.fwl");
        var renamed = Path.Combine(directory.Path, "renamed.fwl");
        WriteWorldFile(original, "Hearthhold", "8f3ha2", 123, UidA);
        WriteWorldFile(renamed, "HearthholdRenamed", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [original]);
        WorldIdentityCatalog.Refresh(cachePath, [renamed]);

        var entry = WorldIdentityCatalog.TryFind(UidA);
        Assert.NotNull(entry);
        Assert.Equal("Hearthhold", entry!.Name);
        Assert.NotNull(entry.ConflictNote);
        Assert.Contains("HearthholdRenamed", entry.ConflictNote);
    }

    [Fact]
    public void Refresh_does_not_flag_a_conflict_when_the_same_identity_is_seen_again()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var original = Path.Combine(directory.Path, "original.fwl");
        var copy = Path.Combine(directory.Path, "copy.fwl");
        WriteWorldFile(original, "Hearthhold", "8f3ha2", 123, UidA);
        WriteWorldFile(copy, "Hearthhold", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [original]);
        WorldIdentityCatalog.Refresh(cachePath, [copy]);

        Assert.Null(WorldIdentityCatalog.TryFind(UidA)!.ConflictNote);
    }

    [Fact]
    public void An_entry_survives_a_later_scan_that_no_longer_finds_the_world_file()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var file = Path.Combine(directory.Path, "a.fwl");
        WriteWorldFile(file, "Hearthhold", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [file]);
        WorldIdentityCatalog.Refresh(cachePath, []); // world's .fwl no longer present locally

        var entry = WorldIdentityCatalog.TryFind(UidA);
        Assert.NotNull(entry);
        Assert.Equal("Hearthhold", entry!.Name);
    }

    [Fact]
    public void Refresh_appends_a_new_filename_to_SeenAsFiles_when_the_same_uid_appears_under_a_different_name()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var original = Path.Combine(directory.Path, "Dragons.fwl");
        var copy = Path.Combine(directory.Path, "GsmTest.fwl");
        // Same name/seed on both — a filesystem copy/rename, confirmed
        // against the real corpus, never
        // touches the wire-stored name — only the filename differs.
        WriteWorldFile(original, "Dragons", "P7yE26tUBj", 123, UidA);
        WriteWorldFile(copy, "Dragons", "P7yE26tUBj", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [original, copy]);

        var entry = WorldIdentityCatalog.TryFind(UidA);
        Assert.NotNull(entry);
        Assert.Null(entry!.ConflictNote);
        Assert.Equal(["Dragons", "GsmTest"], entry.SeenAsFiles.OrderBy(f => f));
    }

    [Fact]
    public void Refresh_does_not_duplicate_a_filename_already_recorded_in_SeenAsFiles()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var file = Path.Combine(directory.Path, "a.fwl");
        WriteWorldFile(file, "Hearthhold", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [file]);
        WorldIdentityCatalog.Refresh(cachePath, [file]); // same file, scanned again

        Assert.Equal(["a"], WorldIdentityCatalog.TryFind(UidA)!.SeenAsFiles);
    }

    [Fact]
    public void SeenAsFiles_survives_a_cache_round_trip()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        var original = Path.Combine(directory.Path, "a.fwl");
        var copy = Path.Combine(directory.Path, "b.fwl");
        WriteWorldFile(original, "Hearthhold", "8f3ha2", 123, UidA);
        WriteWorldFile(copy, "Hearthhold", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [original]);
        WorldIdentityCatalog.Refresh(cachePath, [copy]);
        // A fresh scan pass with nothing new to find — the only way to reach
        // the persisted CSV again is via TryReadCache, since Refresh's
        // in-memory _entries would carry the answer either way.
        WorldIdentityCatalog.Refresh(cachePath, []);

        Assert.Equal(["a", "b"], WorldIdentityCatalog.TryFind(UidA)!.SeenAsFiles.OrderBy(f => f));
    }

    [Fact]
    public void An_unparseable_existing_cache_file_is_left_untouched_on_disk()
    {
        using var directory = TempDirectory.Create();
        var cachePath = Path.Combine(directory.Path, "worlds.csv");
        File.WriteAllText(cachePath, "not,a,valid,cache\nfile");
        var originalContent = File.ReadAllText(cachePath);
        var file = Path.Combine(directory.Path, "a.fwl");
        WriteWorldFile(file, "Hearthhold", "8f3ha2", 123, UidA);

        WorldIdentityCatalog.Refresh(cachePath, [file]);

        Assert.Equal(originalContent, File.ReadAllText(cachePath));
        // Freshly-scanned identities are still usable in-memory this session
        // even though the broken cache file itself was left alone.
        Assert.NotNull(WorldIdentityCatalog.TryFind(UidA));
    }
}
