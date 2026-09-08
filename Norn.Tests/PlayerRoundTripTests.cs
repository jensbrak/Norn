using Norn.GameCore;
using GameVersion = Norn.GameCore.Version;
using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// The corpus harness at L3: <see cref="Player"/> is fully decoded, including
/// inventory, foods, and skills (the three regions L2 held as opaque byte
/// ranges). <see cref="Player.Save"/> is unconditional, matching the game's
/// own — it always writes the current player-data version (33 at 1.0.7) in the full
/// modern shape, exactly like <see cref="PlayerProfile.SavePlayerToDisk()"/> does
/// at the profile level. R1 is therefore scoped the same way as the
/// profile-level harness: only when the on-disk player-data version already
/// equals the writer's target. That target is read from
/// GameVersion.c_PlayerDataVersion rather than hardcoded — it was a literal
/// 29 here until 1.0.7, which made this whole file fail as a stale expectation
/// the moment the mirror was correct.
/// </summary>
/// <remarks>
/// Known, accepted, corpus-untested gap: for a hypothetical player-data
/// version 14-24 file, a food entry's m_health/m_stamina would not survive
/// this round trip — Player.Save only ever writes name+time (the v ≥ 25
/// shape), matching the real game's own permanent information loss on
/// upgrade (see the note on Player.Food). No corpus file is below version 25,
/// so this cannot be exercised here.
/// </remarks>
public class PlayerRoundTripTests
{
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R1_rewriting_player_data_reproduces_it_byte_for_byte(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var profile = new PlayerProfile(path);
        var loaded = profile.Load();

        Assert.SkipWhen(!loaded, $"{fileName} is outside this phase's compatible profile-version range.");
        Assert.SkipWhen(profile.m_playerData is null, $"{fileName} has no inner player-data blob.");

        var original = profile.m_playerData!;

        var player = new Player();
        player.Load(new ZPackage(original));

        Assert.SkipWhen(
            player.PlayerDataVersion != (int)GameVersion.c_PlayerDataVersion,
            $"{fileName} is player-data version {player.PlayerDataVersion}; the writer targets {(int)GameVersion.c_PlayerDataVersion}. "
            + "R1 is only claimed when they match — Player.Save always upgrades to "
            + "the current version, matching the game's own Player.Save.");

        var rewritten = new ZPackage();
        player.Save(rewritten);

        var diff = ByteDiff.Describe(original, rewritten.GetArray());
        Assert.True(diff is null, $"R1 failed for {fileName}.\n{diff}");
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R2_reading_rewritten_player_data_yields_an_equal_graph(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var profile = new PlayerProfile(path);
        var loaded = profile.Load();

        Assert.SkipWhen(!loaded, $"{fileName} is outside this phase's compatible profile-version range.");
        Assert.SkipWhen(profile.m_playerData is null, $"{fileName} has no inner player-data blob.");

        var first = new Player();
        first.Load(new ZPackage(profile.m_playerData!));

        var rewritten = new ZPackage();
        first.Save(rewritten);

        var second = new Player();
        second.Load(new ZPackage(rewritten.GetArray()));

        AssertEqual(first, second, fileName!);
    }

    private static void AssertEqual(Player expected, Player actual, string fileName)
    {
        // Player.Save always writes the current version, so a rewritten file's
        // second read always reports it — regardless of the original.
        Assert.True(actual.PlayerDataVersion == (int)GameVersion.c_PlayerDataVersion, $"R2 failed for {fileName}: rewritten player-data version was {actual.PlayerDataVersion}, expected {(int)GameVersion.c_PlayerDataVersion}.");

        Assert.True(expected.m_maxHealth.Equals(actual.m_maxHealth), $"R2 failed for {fileName}: m_maxHealth differs.");
        Assert.True(expected.m_health.Equals(actual.m_health), $"R2 failed for {fileName}: m_health differs.");
        Assert.True(expected.m_stamina.Equals(actual.m_stamina), $"R2 failed for {fileName}: m_stamina differs.");
        Assert.True(expected.m_maxStamina.Equals(actual.m_maxStamina), $"R2 failed for {fileName}: m_maxStamina differs.");
        Assert.True(expected.m_timeSinceDeath.Equals(actual.m_timeSinceDeath), $"R2 failed for {fileName}: m_timeSinceDeath differs.");
        Assert.True(expected.m_guardianPower == actual.m_guardianPower, $"R2 failed for {fileName}: m_guardianPower differs.");
        Assert.True(expected.m_guardianPowerCooldown.Equals(actual.m_guardianPowerCooldown), $"R2 failed for {fileName}: m_guardianPowerCooldown differs.");

        AssertItemsEqual(expected.m_inventory.m_inventory, actual.m_inventory.m_inventory, fileName);

        AssertListEqual(expected.m_knownRecipes, actual.m_knownRecipes, fileName, nameof(Player.m_knownRecipes));
        AssertPairsEqual(expected.m_knownStations, actual.m_knownStations, fileName, nameof(Player.m_knownStations));
        AssertListEqual(expected.m_knownMaterial, actual.m_knownMaterial, fileName, nameof(Player.m_knownMaterial));
        AssertListEqual(expected.m_shownTutorials, actual.m_shownTutorials, fileName, nameof(Player.m_shownTutorials));
        AssertListEqual(expected.m_uniques, actual.m_uniques, fileName, nameof(Player.m_uniques));
        AssertListEqual(expected.m_trophies, actual.m_trophies, fileName, nameof(Player.m_trophies));
        AssertListEqual(expected.m_knownBiome, actual.m_knownBiome, fileName, nameof(Player.m_knownBiome));
        AssertStringPairsEqual(expected.m_knownTexts, actual.m_knownTexts, fileName, nameof(Player.m_knownTexts));

        Assert.True(expected.m_beardItem == actual.m_beardItem, $"R2 failed for {fileName}: m_beardItem differs.");
        Assert.True(expected.m_hairItem == actual.m_hairItem, $"R2 failed for {fileName}: m_hairItem differs.");
        Assert.Equal(expected.m_skinColor, actual.m_skinColor);
        Assert.Equal(expected.m_hairColor, actual.m_hairColor);
        Assert.True(expected.m_modelIndex == actual.m_modelIndex, $"R2 failed for {fileName}: m_modelIndex differs.");

        AssertFoodsEqual(expected.m_foods, actual.m_foods, fileName);
        AssertSkillsEqual(expected.m_skills.m_skillData, actual.m_skills.m_skillData, fileName);

        AssertStringPairsEqual(expected.m_customData, actual.m_customData, fileName, nameof(Player.m_customData));

        Assert.True(expected.m_eitr.Equals(actual.m_eitr), $"R2 failed for {fileName}: m_eitr differs.");
        Assert.True(expected.m_maxEitr.Equals(actual.m_maxEitr), $"R2 failed for {fileName}: m_maxEitr differs.");
    }

    private static void AssertItemsEqual(
        List<Inventory.ItemData> expected,
        List<Inventory.ItemData> actual,
        string fileName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: inventory item count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];

            // Item version 108 replaced the stored prefab NAME with a one-way
            // stable hash of it, so an item's identity on the wire is now
            // PrefabHash and PrefabName is populated only when the file being
            // read was written at 107 or below. Comparing names across a round
            // trip therefore asks the wrong question: reading a legacy save and
            // rewriting it at 109 correctly produces a file with no name in it,
            // and the name is genuinely unrecoverable from the bytes.
            //
            // The hash IS the identity and it survives exactly — LoadOld
            // computes it from the stored name by the game's own hash function,
            // the writer emits it, and re-reading yields the same value. That is
            // what R2 (graph stability) actually asserts here, and it is a
            // stronger check than the name comparison it replaces, because a
            // wrong hash function would pass a name comparison on a 109 file and
            // fail this one.
            Assert.True(e.PrefabHash == a.PrefabHash, $"R2 failed for {fileName}: item[{i}] PrefabHash differs.");

            // The name still has to survive when it is on the wire at all.
            Assert.True(
                e.PrefabName == "" || a.PrefabName == "" || e.PrefabName == a.PrefabName,
                $"R2 failed for {fileName}: item[{i}] PrefabName differs where both were present.");
            Assert.True(e.m_stack == a.m_stack, $"R2 failed for {fileName}: item[{i}] m_stack differs.");
            // Item version 108 quantised durability: it is written as
            // (int)(d * 100f) and read back as that * 0.01f, where a legacy
            // save stored the full float. So reading a pre-108 file and
            // rewriting it at 109 loses precision *by format*, permanently and
            // by the game's own design — the same shape as the already-accepted
            // food m_health/m_stamina loss documented on this class.
            //
            // Asserting exact equality here would demand something the format
            // cannot deliver. Asserting a tolerance would let a genuinely wrong
            // writer through. So this asserts the exact value the quantisation
            // is *supposed* to produce, which stays a precise check: any error
            // in the truncating cast, the multiplier, or the read-back factor
            // still fails.
            var quantised = (int)(e.m_durability * 100f) * 0.01f;
            Assert.True(
                a.m_durability.Equals(quantised),
                $"R2 failed for {fileName}: item[{i}] m_durability was {a.m_durability:R}, "
                + $"expected {quantised:R} (the item-version-108 quantisation of {e.m_durability:R}).");
            Assert.Equal(e.m_gridPos, a.m_gridPos);
            Assert.True(e.m_equipped == a.m_equipped, $"R2 failed for {fileName}: item[{i}] m_equipped differs.");
            Assert.True(e.m_quality == a.m_quality, $"R2 failed for {fileName}: item[{i}] m_quality differs.");
            Assert.True(e.m_variant == a.m_variant, $"R2 failed for {fileName}: item[{i}] m_variant differs.");
            Assert.True(e.m_crafterID == a.m_crafterID, $"R2 failed for {fileName}: item[{i}] m_crafterID differs.");
            Assert.True(e.m_crafterName == a.m_crafterName, $"R2 failed for {fileName}: item[{i}] m_crafterName differs.");
            AssertStringPairsEqual(e.m_customData, a.m_customData, fileName, $"item[{i}].m_customData");
            Assert.True(e.m_worldLevel == a.m_worldLevel, $"R2 failed for {fileName}: item[{i}] m_worldLevel differs.");
            Assert.True(e.m_pickedUp == a.m_pickedUp, $"R2 failed for {fileName}: item[{i}] m_pickedUp differs.");
        }
    }

    private static void AssertFoodsEqual(List<Player.Food> expected, List<Player.Food> actual, string fileName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: food count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].m_name == actual[i].m_name, $"R2 failed for {fileName}: food[{i}] m_name differs.");
            Assert.True(expected[i].m_time.Equals(actual[i].m_time), $"R2 failed for {fileName}: food[{i}] m_time differs.");
        }
    }

    private static void AssertSkillsEqual(List<Skills.Skill> expected, List<Skills.Skill> actual, string fileName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: skill count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].m_type == actual[i].m_type, $"R2 failed for {fileName}: skill[{i}] m_type differs.");
            Assert.True(expected[i].m_level.Equals(actual[i].m_level), $"R2 failed for {fileName}: skill[{i}] m_level differs.");
            Assert.True(expected[i].m_accumulator.Equals(actual[i].m_accumulator), $"R2 failed for {fileName}: skill[{i}] m_accumulator differs.");
        }
    }

    private static void AssertListEqual<T>(List<T> expected, List<T> actual, string fileName, string fieldName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: {fieldName} count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(
                EqualityComparer<T>.Default.Equals(expected[i], actual[i]),
                $"R2 failed for {fileName}: {fieldName}[{i}] differs (expected {expected[i]}, got {actual[i]}).");
        }
    }

    private static void AssertPairsEqual(
        List<KeyValuePair<string, int>> expected,
        List<KeyValuePair<string, int>> actual,
        string fileName,
        string fieldName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: {fieldName} count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(
                expected[i].Key == actual[i].Key && expected[i].Value == actual[i].Value,
                $"R2 failed for {fileName}: {fieldName}[{i}] differs (expected {expected[i]}, got {actual[i]}).");
        }
    }

    private static void AssertStringPairsEqual(
        List<KeyValuePair<string, string>> expected,
        List<KeyValuePair<string, string>> actual,
        string fileName,
        string fieldName)
    {
        Assert.True(expected.Count == actual.Count, $"R2 failed for {fileName}: {fieldName} count differs ({expected.Count} vs {actual.Count}).");

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(
                expected[i].Key == actual[i].Key && expected[i].Value == actual[i].Value,
                $"R2 failed for {fileName}: {fieldName}[{i}] differs (expected {expected[i]}, got {actual[i]}).");
        }
    }
}
