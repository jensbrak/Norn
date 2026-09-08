using Norn.Adapter;
using Norn.GameCore;
using Norn.UI;
using GameVersion = Norn.GameCore.Version;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="World.Load"/> against real <c>.fwl</c> files — a
/// theory over <see cref="WorldCorpus.Files"/>, same shape as the <c>.fch</c>
/// corpus theories, skipping cleanly when the corpus is absent. Unlike
/// the character-save
/// corpus, there is no R1/R2 round-trip to assert here — this is a read-only
/// mirror with no write path (World.cs's own provenance header) — so this
/// only confirms every real file the corpus contains actually parses and
/// yields plausible values, not byte-for-byte anything.
/// </summary>
public class WorldCorpusTests
{
    public static IEnumerable<object[]> WorldFixtures() =>
        TestPaths.WorldFixtureFiles().Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>
    /// Same reasoning as <see cref="FixtureRoundTripTests.The_fixture_set_is_actually_present"/>:
    /// unlike the gitignored real corpus above, the tracked <c>.fwl</c>
    /// fixture is never expected to be empty, so its absence is a loud
    /// failure, not a silent skip.
    /// </summary>
    [Fact]
    public void The_world_fixture_set_is_actually_present()
    {
        Assert.NotEmpty(TestPaths.WorldFixtureFiles());
    }

    /// <summary>
    /// Real <c>.fwl</c>/<see cref="World.Load"/> coverage on a fresh clone
    /// with no gitignored world corpus — <see cref="Loads_every_corpus_world_file_with_a_non_empty_name_and_uid"/>
    /// skips entirely in that state, which used to leave zero real coverage
    /// of this path despite a real <c>.fwl</c> fixture already being
    /// committed for exactly this purpose (found in review).
    /// </summary>
    [Theory]
    [MemberData(nameof(WorldFixtures))]
    public void Loads_every_world_fixture_with_a_non_empty_name_and_uid(string fileName)
    {
        var path = Path.Combine(TestPaths.FixtureDirectory!, fileName);

        var payload = World.ReadPayloadFromDisk(path);
        var world = new World();
        var loaded = world.Load(payload);

        Assert.True(loaded, $"{fileName} failed to load (incompatible version?).");
        Assert.False(string.IsNullOrEmpty(world.m_name), $"{fileName} loaded with an empty name.");
        Assert.NotEqual(0L, world.m_uid);
    }

    [Theory]
    [MemberData(nameof(WorldCorpus.Files), MemberType = typeof(WorldCorpus))]
    public void Loads_every_corpus_world_file_with_a_non_empty_name_and_uid(string? fileName)
    {
        var path = WorldCorpus.RequireFile(fileName);

        var payload = World.ReadPayloadFromDisk(path);
        var world = new World();
        var loaded = world.Load(payload);

        Assert.True(loaded, $"{fileName} failed to load (incompatible version?).");
        Assert.False(string.IsNullOrEmpty(world.m_name), $"{fileName} loaded with an empty name.");
        Assert.NotEqual(0L, world.m_uid);
    }

    /// <summary>
    /// Confirms <see cref="WorldFileLocator"/> excludes real, naturally
    /// occurring backup files (a real corpus includes several — both
    /// the timestamp and the <c>_auto-</c> shapes) rather than only synthetic
    /// filenames (<c>WorldFileLocatorTests</c>). A manually-named variant
    /// with no <c>_backup_</c> marker (e.g. <c>BoggaBogga_just-started.fwl</c>)
    /// is deliberately expected to survive filtering — matching the
    /// documented "does not recognize manually renamed files" contract.
    /// </summary>
    [Fact]
    public void WorldFileLocator_excludes_every_real_backup_shaped_file_in_the_corpus()
    {
        var directory = TestPaths.WorldCorpusDirectory;
        Assert.SkipWhen(directory is null, "World-file corpus not present at reference/world-files; it is gitignored by design.");

        // Unfiltered, deliberately: WorldCorpusFiles() is the locator's own
        // output, so asserting against it would be circular — anything the
        // locator excluded is already absent from it.
        var allFiles = TestPaths.WorldCorpusFilesUnfiltered();
        var backupShaped = allFiles.Where(f => WorldFileClassifier.IsBackupFile(Path.GetFileName(f))).ToList();
        // Sanity check on the corpus itself, not the code under test: if this
        // ever fails, the corpus stopped containing backups and the rest of
        // this test is not actually exercising the exclusion path.
        Assert.NotEmpty(backupShaped);

        var found = WorldFileLocator.FindWorldFiles([directory!]);

        Assert.DoesNotContain(found, f => WorldFileClassifier.IsBackupFile(Path.GetFileName(f)));
        Assert.DoesNotContain(found, WorldFileClassifier.IsBackupPath);

        // Chunked worlds keep only their highest save number, so the count is
        // not simply "everything minus the backups" any more.
        var superseded = allFiles
            .Where(f => WorldFileClassifier.IsChunkedWorldFile(Path.GetFileName(f)))
            .GroupBy(f => Path.GetDirectoryName(f))
            .Sum(g => g.Count() - 1);

        Assert.Equal(allFiles.Count - backupShaped.Count - superseded, found.Count);
    }

    /// <summary>
    /// The Valheim 1.0 layout is found at all, and filed under the world's own
    /// name rather than the file's.
    /// </summary>
    /// <remarks>
    /// This is the regression the 1.0.7 upgrade existed to close: every 1.0
    /// world lives at <c>&lt;dir&gt;/Name/_main.&lt;N&gt;.fwl2</c>, and the
    /// pre-upgrade locator enumerated flat <c>.fwl</c> files only. It did not
    /// fail — it returned an empty list, which is indistinguishable from "this
    /// user has no worlds". Skips rather than fails when the corpus holds no
    /// chunked world, so the suite stays honest about what it actually
    /// covered.
    /// </remarks>
    [Fact]
    public void Finds_chunked_worlds_and_names_them_by_their_directory()
    {
        var directory = TestPaths.WorldCorpusDirectory;
        Assert.SkipWhen(directory is null, "World-file corpus not present at reference/world-files; it is gitignored by design.");

        var found = WorldFileLocator.FindWorldFiles([directory!]);
        var chunked = found.Where(f => WorldFileClassifier.IsChunkedWorldFile(Path.GetFileName(f))).ToList();
        Assert.SkipWhen(chunked.Count == 0, "World corpus contains no Valheim 1.0 (chunked) world.");

        foreach (var path in chunked)
        {
            var saveName = WorldFileClassifier.GetWorldSaveName(path);

            Assert.False(
                string.IsNullOrEmpty(saveName),
                $"{path} produced an empty save name.");
            Assert.DoesNotContain("_main", saveName);
            Assert.Equal(Path.GetFileName(Path.GetDirectoryName(path)), saveName);

            var world = new World();
            Assert.True(world.Load(World.ReadPayloadFromDisk(path)), $"{path} failed to load.");
            Assert.Equal((int)GameVersion.c_WorldVersion, world.m_worldVersion);
        }
    }

    /// <summary>
    /// Only the newest save number per world directory is reported.
    /// </summary>
    /// <remarks>
    /// The game increments the save number on every write and leaves the
    /// previous file behind, so a directory can legitimately hold several
    /// <c>_main.&lt;N&gt;.fwl2</c>. Reporting all of them would surface the same
    /// world repeatedly as slightly different snapshots, which the identity
    /// catalog would then read as a naming conflict.
    /// </remarks>
    [Fact]
    public void Reports_one_file_per_chunked_world_directory()
    {
        var directory = TestPaths.WorldCorpusDirectory;
        Assert.SkipWhen(directory is null, "World-file corpus not present at reference/world-files; it is gitignored by design.");

        var found = WorldFileLocator.FindWorldFiles([directory!])
            .Where(f => WorldFileClassifier.IsChunkedWorldFile(Path.GetFileName(f)))
            .ToList();
        Assert.SkipWhen(found.Count == 0, "World corpus contains no Valheim 1.0 (chunked) world.");

        var perDirectory = found.GroupBy(Path.GetDirectoryName).ToList();
        Assert.All(perDirectory, g => Assert.Single(g));

        // And the one reported is the highest-numbered present on disk.
        foreach (var group in perDirectory)
        {
            var highest = Directory.EnumerateFiles(group.Key!)
                .Select(p => WorldFileClassifier.TryGetChunkedSaveNumber(Path.GetFileName(p)))
                .Where(n => n is not null)
                .Max();

            Assert.Equal(highest, WorldFileClassifier.TryGetChunkedSaveNumber(Path.GetFileName(group.Single())));
        }
    }
}
