using Norn.Adapter;
using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Exercises every mapper against the corpus, confirming each DTO field
/// reachable from a loaded <see cref="PlayerProfile"/>/<see cref="Player"/> pair
/// carries the same value the mirror read. Tagged with
/// <see cref="CatalogCollection"/>: five Unlockables fields now resolve
/// through <see cref="LocalizationCatalog"/>/<see cref="SharedItemDataCatalog"/>
/// loaded here from the real tracked resources — expected
/// values are computed via the exact same TryFind-or-fall-back-to-raw-key
/// pattern <see cref="UnlockablesMapper"/> itself uses (<see cref="Resolve"/>/
/// <see cref="ResolveTrophy"/>), so the assertions hold whether or not those
/// resource files happen to be present (both sides degrade identically).
/// </summary>
[Collection(CatalogCollection.Name)]
public class AdapterMappingTests
{
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Mappers_reproduce_loaded_profile_and_player_fields(string? fileName)
    {
        LocalizationCatalog.Load(TestPaths.LocalizationDataCsvPath);
        SharedItemDataCatalog.Load(TestPaths.SharedItemDataCsvPath);

        var path = Corpus.RequireFile(fileName);

        var profile = new PlayerProfile(path);
        var loaded = profile.Load();

        Assert.SkipWhen(!loaded, $"{fileName} is outside the compatible profile-version range.");

        var player = PlayerLoader.Load(profile);

        var meta = MetaMapper.Map(profile, player);
        Assert.Equal(profile.m_playerName, meta.PlayerName);
        Assert.Equal(profile.m_playerID, meta.PlayerId);
        Assert.Equal(profile.m_startSeed, meta.StartSeed);
        Assert.Equal(profile.ProfileVersion, meta.ProfileVersion);
        Assert.Equal(profile.m_dateCreated, meta.DateCreated);
        Assert.Equal(profile.m_usedCheats, meta.UsedCheats);
        Assert.Equal(profile.m_firstSpawn, meta.FirstSpawn);
        Assert.Equal(player?.PlayerDataVersion ?? 0, meta.PlayerDataVersion);
        Assert.Equal(player?.m_beardItem ?? "", meta.BeardItem);
        Assert.Equal(player?.m_hairItem ?? "", meta.HairItem);
        Assert.Equal(player?.m_modelIndex ?? 0, meta.ModelIndex);
        if (player is not null)
        {
            Assert.Equal(player.m_skinColor.x, meta.SkinColor.R);
            Assert.Equal(player.m_skinColor.y, meta.SkinColor.G);
            Assert.Equal(player.m_skinColor.z, meta.SkinColor.B);
            Assert.Equal(player.m_hairColor.x, meta.HairColor.R);
            Assert.Equal(player.m_hairColor.y, meta.HairColor.G);
            Assert.Equal(player.m_hairColor.z, meta.HairColor.B);
        }

        var statistics = StatisticsMapper.Map(profile);
        // PlayerStatType.Count (105) is one past the range PlayerStats..ctor
        // actually seeds (indices 0..104, a bare literal loop bound — see the
        // note on PlayerProfile.PlayerStats), so it is never a key in
        // m_stats at all; the mapper's own filter guards a case that cannot
        // currently occur, kept as a safety net rather than a live path.
        // All ten slots, each compared against the profile slot it came from —
        // not just slot 0, which would leave nine tenths mapped but unverified.
        // The per-collection assertion is main's AssertResolvedPairsEqual, which
        // compares names AND values rather than counts: comparing lengths alone
        // would pass an implementation that zeroed every statistic or resolved
        // every key to the same string. Applying it across all ten slots is the
        // union of both sides of this merge, and stronger than either.
        Assert.Equal(profile.m_playerStats.Length, statistics.Slots.Count);
        for (var slot = 0; slot < profile.m_playerStats.Length; slot++)
        {
            var expectedSlot = profile.m_playerStats[slot];
            var actualSlot = statistics.Slots[slot];

            Assert.Equal((StatisticsDifficulty)slot, actualSlot.Difficulty);
            Assert.DoesNotContain(actualSlot.PlayerStats, stat => stat.Name == nameof(PlayerStatType.Count));
            Assert.DoesNotContain(actualSlot.PlayerStats, stat => stat.Name == nameof(PlayerStatType.None));

            Assert.Equal(expectedSlot.m_enemyStats.Length, actualSlot.EnemyStats.Count);
            for (var kill = 0; kill < expectedSlot.m_enemyStats.Length; kill++)
            {
                AssertResolvedPairsEqual(
                    expectedSlot.m_enemyStats[kill], actualSlot.EnemyStats[kill], $"m_enemyStats[{slot}][{kill}]");
            }

            AssertResolvedPairsEqual(expectedSlot.m_itemPickupStats, actualSlot.ItemPickupStats, $"m_itemPickupStats[{slot}]");
            AssertResolvedPairsEqual(expectedSlot.m_itemCraftStats, actualSlot.ItemCraftStats, $"m_itemCraftStats[{slot}]");
            AssertResolvedPairsEqual(expectedSlot.m_pickableStats, actualSlot.PickableStats, $"m_pickableStats[{slot}]");
            AssertResolvedPairsEqual(expectedSlot.m_foodEatenStats, actualSlot.FoodEatenStats, $"m_foodEatenStats[{slot}]");
            AssertResolvedPairsEqual(expectedSlot.m_piecesPlacedStats, actualSlot.PiecesPlacedStats, $"m_piecesPlacedStats[{slot}]");
        }

        var worlds = WorldsMapper.Map(profile);
        Assert.Equal(profile.m_worldData.Count, worlds.Worlds.Count);
        for (var i = 0; i < profile.m_worldData.Count; i++)
        {
            var expected = profile.m_worldData[i];
            var actual = worlds.Worlds[i];

            Assert.Equal(expected.Key, actual.WorldId);
            Assert.Equal(expected.Value.m_haveCustomSpawnPoint, actual.HaveCustomSpawnPoint);
            Assert.Equal(expected.Value.m_spawnPoint.x, actual.SpawnPoint.X);
            Assert.Equal(expected.Value.m_spawnPoint.y, actual.SpawnPoint.Y);
            Assert.Equal(expected.Value.m_spawnPoint.z, actual.SpawnPoint.Z);
            Assert.Equal(expected.Value.m_mapData is not null, actual.HasMapData);
            Assert.Equal(WorldIdentityCatalog.TryFind(expected.Key), actual.Identity);
        }
        Assert.Equal(profile.m_playerStats[0].m_knownWorlds.Count, worlds.KnownWorlds.Count);

        Assert.SkipWhen(player is null, $"{fileName} has no inner player-data blob.");

        var skills = SkillsMapper.Map(player!);
        Assert.Equal(player!.m_skills.m_skillData.Count, skills.Skills.Count);
        for (var i = 0; i < skills.Skills.Count; i++)
        {
            Assert.Equal(player.m_skills.m_skillData[i].m_type.ToString(), skills.Skills[i].Type);
            Assert.Equal(player.m_skills.m_skillData[i].m_level, skills.Skills[i].Level);
        }

        var vitals = VitalsMapper.Map(player);
        Assert.Equal(player.m_health, vitals.Health);
        Assert.Equal(player.m_maxHealth, vitals.MaxHealth);
        Assert.Equal(player.m_stamina, vitals.Stamina);
        Assert.Equal(player.m_eitr, vitals.Eitr);
        Assert.Equal(player.m_foods.Count, vitals.ActiveFoods.Count);

        var inventory = InventoryMapper.Map(player);
        Assert.Equal(player.m_inventory.m_inventory.Count, inventory.Items.Count);
        for (var i = 0; i < inventory.Items.Count; i++)
        {
            var expected = player.m_inventory.m_inventory[i];
            var actual = inventory.Items[i];

            // Every ItemDto field, not the four this used to check (found in
            // review): the omitted ones (durability, quality, variant,
            // crafter, equipped, world level, picked-up) are exactly the
            // fields the inventory mutators write, so a mapping bug in any
            // of them would have gone unnoticed here.
            //
            // The name is the one field that cannot be compared straight
            // through any more. From item version 108 a save carries only a
            // hash, so GameCore's PrefabName is empty and the DTO's name is
            // resolved from that hash against the catalog; asserting equality
            // with the raw field would be asserting the pre-1.0 contract.
            Assert.Equal(
                ItemPrefabHashes.Resolve(expected.PrefabName, expected.PrefabHash),
                actual.PrefabName);

            // And the resolution has to actually produce something for a file
            // written at 108+, or the inventory silently degrades to a list of
            // nameless rows — the exact failure the resolution exists to
            // prevent. Guarded on the hash being known to the catalog, since a
            // modded or newly-added item legitimately resolves to "".
            if (expected.PrefabName == "" && ItemPrefabHashes.TryResolve(expected.PrefabHash) is not null)
            {
                Assert.NotEqual("", actual.PrefabName);
            }

            Assert.Equal(expected.m_stack, actual.Stack);
            Assert.Equal(expected.m_durability, actual.Durability);
            Assert.Equal(expected.m_gridPos.x, actual.GridX);
            Assert.Equal(expected.m_gridPos.y, actual.GridY);
            Assert.Equal(expected.m_equipped, actual.Equipped);
            Assert.Equal(expected.m_quality, actual.Quality);
            Assert.Equal(expected.m_variant, actual.Variant);
            Assert.Equal(expected.m_crafterID, actual.CrafterId);
            Assert.Equal(expected.m_crafterName, actual.CrafterName);
            Assert.Equal(expected.m_worldLevel, actual.WorldLevel);
            Assert.Equal(expected.m_pickedUp, actual.PickedUp);
            Assert.Equal(expected.m_customData.Count, actual.CustomData.Count);
        }

        var unlockables = UnlockablesMapper.Map(player);
        Assert.Equal(
            player.m_knownRecipes.Select(Resolve).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal),
            unlockables.KnownRecipes);
        Assert.Equal(
            player.m_knownStations.Select(p => (Resolve(p.Key), p.Value)).Where(p => p.Item1.Length > 0)
                .OrderBy(p => p.Item1, StringComparer.Ordinal),
            unlockables.KnownStations.Select(s => (s.Name, s.Level)));
        Assert.Equal(
            player.m_knownMaterial.Select(Resolve).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal),
            unlockables.KnownMaterial);
        Assert.Equal(
            player.m_uniques.Select(Sanitize).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal),
            unlockables.Uniques);
        Assert.Equal(
            player.m_trophies.Select(Sanitize).Where(s => s.Length > 0).Select(ResolveTrophy)
                .OrderBy(s => s, StringComparer.Ordinal),
            unlockables.Trophies);
        Assert.Equal(
            player.m_knownBiome.Select(b => b.ToString()).OrderBy(s => s, StringComparer.Ordinal),
            unlockables.KnownBiomes);
        Assert.Equal(
            player.m_knownTexts.Select(p => (Resolve(p.Key), Resolve(p.Value))).Where(p => p.Item1.Length > 0)
                .OrderBy(p => p.Item1, StringComparer.Ordinal),
            unlockables.KnownTexts.Select(t => (t.Label, t.Text)));
        Assert.Equal(
            player.m_shownTutorials.Select(Sanitize).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal),
            unlockables.ShownTutorials);
    }

    private static string Sanitize(string raw)
        => raw.Any(char.IsControl) ? new string(raw.Where(c => !char.IsControl(c)).ToArray()) : raw;

    /// <summary>Asserts one resolved stat collection matches the source
    /// pairs in order, by name and value — the mapper preserves source order
    /// for these, unlike the Unlockables collections it sorts.</summary>
    private static void AssertResolvedPairsEqual(
        List<KeyValuePair<string, float>> expected,
        IReadOnlyList<StatDto> actual,
        string fieldName)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(Resolve(expected[i].Key), actual[i].Name);
            Assert.Equal(expected[i].Value, actual[i].Value);
        }
    }

    private static string Resolve(string key) => LocalizationCatalog.TryFind(Sanitize(key)) ?? Sanitize(key);

    private static string ResolveTrophy(string name) => SharedItemDataCatalog.TryFind(name)?.DisplayName ?? name;
}
