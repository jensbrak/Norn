using Norn.GameCore;
using GameVersion = Norn.GameCore.Version;

namespace Norn.Tests;

/// <summary>
/// The corpus harness at L1: the profile envelope's own fields are parsed;
/// the inner player-data blob stays opaque (byte[]) until L2.
/// </summary>
/// <remarks>
/// <para>
/// R1 here is narrower than at L0. The game's writer always emits its own
/// current version literal, discarding whatever version was read — a real save
/// upgrade, not a bug — so a rewritten file is only byte-identical to the
/// original when the original was already at the writer's version. For every
/// other corpus version, only R2 (graph stability) is claimed; the write path
/// is unconditional rather than version-gated, matching the game's own writer.
/// </para>
/// <para>
/// A file whose profile version falls outside the mirror's own compatible
/// range is refused by <see cref="PlayerProfile.Load"/>, exactly as a real client
/// game client would refuse them. That is not a defect to work around here —
/// R1/R2 simply do not apply to a file the mirror correctly refuses to load,
/// so both tests skip rather than fail for those.
/// </para>
/// </remarks>
public class ProfileRoundTripTests
{
    public static IEnumerable<object[]> Fixtures() =>
        TestPaths.FixtureFiles().Select(path => new object[] { Path.GetFileName(path) });

    /// <summary>
    /// Same field-level assertions as
    /// <see cref="R2_reading_a_rewritten_profile_yields_an_equal_graph"/>,
    /// against the tracked fixture set instead of the gitignored real
    /// corpus. On a fresh clone with no corpus, that test skips entirely for
    /// every file, leaving <see cref="FixtureRoundTripTests"/>' envelope-only
    /// R1/R2 as the only test that actually runs against real fixtures — but
    /// it never parses fields, so a version-gated regression in
    /// <see cref="PlayerProfile.LoadPlayerFromDisk"/> (e.g. one of its
    /// <c>ver &gt;= 38</c>/<c>ver &gt;= 42</c> branches) could ship
    /// undetected on such a clone (found in review). Fixtures are never
    /// expected to be empty, so this never skips — reuses
    /// <see cref="AssertEqual"/> rather than duplicating its ~80 lines of
    /// field-by-field comparison.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void R2_reading_a_rewritten_fixture_profile_yields_an_equal_graph(string fileName)
    {
        var path = Path.Combine(TestPaths.FixtureDirectory!, fileName);

        var first = new PlayerProfile(path);
        Assert.True(first.Load(), $"{fileName} failed to load.");

        using var rewritten = TempFile.Create();
        first.m_filename = rewritten.Path;
        first.SavePlayerToDisk();

        var second = new PlayerProfile(rewritten.Path);
        Assert.True(second.Load(), $"{fileName}'s rewritten file failed to load.");

        AssertEqual(first, second, fileName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R1_when_profile_version_matches_writer_version(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var profile = new PlayerProfile(path);
        var loaded = profile.Load();

        Assert.SkipWhen(
            !loaded,
            $"{fileName} is profile version {profile.ProfileVersion}, outside this phase's "
            + $"compatible range [{(int)GameVersion.Player.OldestForwardCompatible}, "
            + $"{(int)GameVersion.c_PlayerVersion}]. The mirror correctly refuses to load it, "
            + "matching a real client.");

        Assert.SkipWhen(
            profile.ProfileVersion != (int)GameVersion.c_PlayerVersion,
            $"{fileName} is profile version {profile.ProfileVersion}; our writer targets "
            + $"{(int)GameVersion.c_PlayerVersion}. R1 is only claimed when they match "
            + "— the game's own writer always upgrades to its current version on save, so "
            + "R2 is the applicable oracle for every other version.");

        var original = File.ReadAllBytes(path);

        using var rewritten = TempFile.Create();
        profile.m_filename = rewritten.Path;
        profile.SavePlayerToDisk();

        var diff = ByteDiff.Describe(original, File.ReadAllBytes(rewritten.Path));
        Assert.True(diff is null, $"R1 failed for {fileName}.\n{diff}");
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R2_reading_a_rewritten_profile_yields_an_equal_graph(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var first = new PlayerProfile(path);
        var loaded = first.Load();

        Assert.SkipWhen(
            !loaded,
            $"{fileName} is profile version {first.ProfileVersion}, outside this phase's "
            + $"compatible range [{(int)GameVersion.Player.OldestForwardCompatible}, "
            + $"{(int)GameVersion.c_PlayerVersion}]. The mirror correctly refuses to load it, "
            + "matching a real client.");

        using var rewritten = TempFile.Create();
        first.m_filename = rewritten.Path;
        first.SavePlayerToDisk();

        var second = new PlayerProfile(rewritten.Path);
        Assert.True(second.Load(), $"{fileName}'s rewritten file failed to load.");

        AssertEqual(first, second, fileName!);
    }

    private static void AssertEqual(PlayerProfile expected, PlayerProfile actual, string fileName)
    {
        // The writer always emits its own current version literal, regardless of
        // what expected.ProfileVersion was.
        Assert.True(
            actual.ProfileVersion == (int)GameVersion.c_PlayerVersion,
            $"R2 failed for {fileName}: rewritten profile version was {actual.ProfileVersion}, "
            + $"expected the writer version {(int)GameVersion.c_PlayerVersion}.");

        AssertStatsEqual(expected.m_playerStats, actual.m_playerStats, fileName);

        Assert.True(expected.m_firstSpawn == actual.m_firstSpawn, $"R2 failed for {fileName}: m_firstSpawn differs.");
        Assert.True(expected.m_playerName == actual.m_playerName, $"R2 failed for {fileName}: m_playerName differs.");
        Assert.True(expected.m_playerID == actual.m_playerID, $"R2 failed for {fileName}: m_playerID differs.");
        Assert.True(expected.m_startSeed == actual.m_startSeed, $"R2 failed for {fileName}: m_startSeed differs.");
        Assert.True(expected.m_usedCheats == actual.m_usedCheats, $"R2 failed for {fileName}: m_usedCheats differs.");
        Assert.True(expected.m_dateCreated == actual.m_dateCreated, $"R2 failed for {fileName}: m_dateCreated differs.");
        Assert.True(
            expected.DateCreatedUnixSeconds == actual.DateCreatedUnixSeconds,
            $"R2 failed for {fileName}: DateCreatedUnixSeconds differs.");

        var playerDataDiff = ByteDiff.Describe(expected.m_playerData ?? [], actual.m_playerData ?? []);
        Assert.True(
            (expected.m_playerData is null) == (actual.m_playerData is null) && playerDataDiff is null,
            $"R2 failed for {fileName}: m_playerData differs.\n{playerDataDiff}");

        AssertWorldDataEqual(expected.m_worldData, actual.m_worldData, fileName);

        // Valheim 1.0 moved these six collections off PlayerProfile and into
        // each of the ten PlayerStats slots, and added three more. Comparing
        // slot 0 alone would still compile and would silently stop testing
        // nine tenths of the data, so every slot is compared — this assertion
        // got wider at 1.0.7, not narrower.
        for (var slot = 0; slot < expected.m_playerStats.Length; slot++)
        {
            var e = expected.m_playerStats[slot];
            var a = actual.m_playerStats[slot];

            AssertPairsEqual(e.m_knownWorlds, a.m_knownWorlds, fileName, $"m_knownWorlds[{slot}]");
            AssertPairsEqual(e.m_knownWorldKeys, a.m_knownWorldKeys, fileName, $"m_knownWorldKeys[{slot}]");
            AssertPairsEqual(e.m_knownCommands, a.m_knownCommands, fileName, $"m_knownCommands[{slot}]");

            for (var kill = 0; kill < e.m_enemyStats.Length; kill++)
            {
                AssertPairsEqual(
                    e.m_enemyStats[kill], a.m_enemyStats[kill], fileName, $"m_enemyStats[{slot}][{kill}]");
            }

            AssertPairsEqual(e.m_itemPickupStats, a.m_itemPickupStats, fileName, $"m_itemPickupStats[{slot}]");
            AssertPairsEqual(e.m_itemCraftStats, a.m_itemCraftStats, fileName, $"m_itemCraftStats[{slot}]");
            AssertPairsEqual(e.m_pickableStats, a.m_pickableStats, fileName, $"m_pickableStats[{slot}]");
            AssertPairsEqual(e.m_foodEatenStats, a.m_foodEatenStats, fileName, $"m_foodEatenStats[{slot}]");
            AssertPairsEqual(e.m_piecesPlacedStats, a.m_piecesPlacedStats, fileName, $"m_piecesPlacedStats[{slot}]");
        }
    }

    // Compares every one of the ten stat slots, for the same reason the
    // collection comparison above does.
    private static void AssertStatsEqual(
        PlayerProfile.PlayerStats[] expected, PlayerProfile.PlayerStats[] actual, string fileName)
    {
        Assert.True(
            expected.Length == actual.Length,
            $"R2 failed for {fileName}: stat slot count differs ({expected.Length} vs {actual.Length}).");

        for (var slot = 0; slot < expected.Length; slot++)
        {
            Assert.True(
                expected[slot].m_stats.Count == actual[slot].m_stats.Count,
                $"R2 failed for {fileName}: stat count differs in slot {slot} "
                + $"({expected[slot].m_stats.Count} vs {actual[slot].m_stats.Count}).");

            foreach (var pair in expected[slot].m_stats)
            {
                Assert.True(
                    actual[slot].m_stats.TryGetValue(pair.Key, out var actualValue) && actualValue == pair.Value,
                    $"R2 failed for {fileName}: stat {pair.Key} differs in slot {slot}.");
            }
        }
    }

    private static void AssertWorldDataEqual(
        List<KeyValuePair<long, PlayerProfile.WorldPlayerData>> expected,
        List<KeyValuePair<long, PlayerProfile.WorldPlayerData>> actual,
        string fileName)
    {
        Assert.True(
            expected.Count == actual.Count,
            $"R2 failed for {fileName}: world-data count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Key == actual[i].Key, $"R2 failed for {fileName}: world UID at index {i} differs.");

            var e = expected[i].Value;
            var a = actual[i].Value;

            Assert.True(e.m_haveCustomSpawnPoint == a.m_haveCustomSpawnPoint, $"R2 failed for {fileName}: world {expected[i].Key} m_haveCustomSpawnPoint differs.");
            Assert.Equal(e.m_spawnPoint, a.m_spawnPoint);
            Assert.True(e.m_haveLogoutPoint == a.m_haveLogoutPoint, $"R2 failed for {fileName}: world {expected[i].Key} m_haveLogoutPoint differs.");
            Assert.Equal(e.m_logoutPoint, a.m_logoutPoint);
            Assert.True(e.m_haveDeathPoint == a.m_haveDeathPoint, $"R2 failed for {fileName}: world {expected[i].Key} m_haveDeathPoint differs.");
            Assert.Equal(e.m_deathPoint, a.m_deathPoint);
            Assert.Equal(e.m_homePoint, a.m_homePoint);

            var mapDataDiff = ByteDiff.Describe(e.m_mapData ?? [], a.m_mapData ?? []);
            Assert.True(
                (e.m_mapData is null) == (a.m_mapData is null) && mapDataDiff is null,
                $"R2 failed for {fileName}: world {expected[i].Key} m_mapData differs.\n{mapDataDiff}");
        }
    }

    private static void AssertPairsEqual(
        List<KeyValuePair<string, float>> expected,
        List<KeyValuePair<string, float>> actual,
        string fileName,
        string fieldName)
    {
        Assert.True(
            expected.Count == actual.Count,
            $"R2 failed for {fileName}: {fieldName} count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(
                expected[i].Key == actual[i].Key && expected[i].Value.Equals(actual[i].Value),
                $"R2 failed for {fileName}: {fieldName}[{i}] differs (expected {expected[i]}, got {actual[i]}).");
        }
    }
}
