using Norn.Adapter;
using Norn.GameCore;
using GameVersion = Norn.GameCore.Version;

namespace Norn.Tests;

/// <summary>
/// Norn must never damage a save it does not fully understand.
/// </summary>
/// <remarks>
/// <para>
/// From item version 108 a save stores only a one-way hash of each item's
/// prefab name, so Norn can only name an item whose hash appears in the
/// catalog it ships (<c>Norn.UI/Content/SharedItemData.csv</c>, produced by an
/// external extraction tool). That catalog is not guaranteed complete: it lags
/// the game by however long it takes to re-run the extraction, the extraction
/// itself can be wrong, and a modded save can contain items no extraction will
/// ever have.
/// </para>
/// <para>
/// "Cannot name it" must therefore mean exactly one thing — the item shows
/// without a name — and never any of: dropped, renamed, blanked, or written
/// back with a different identity. These tests pin that by running the whole
/// load/save path with an <b>empty</b> catalog, which is the worst case the
/// extraction can produce, and asserting the bytes are untouched.
/// </para>
/// </remarks>
[Collection(CatalogCollection.Name)]
public class UnresolvedItemSafetyTests
{
    /// <summary>
    /// The strongest form of the guarantee: with no catalog at all, a save
    /// that Norn opens and re-saves is byte-identical. Scoped to files whose
    /// on-disk version already equals the writer's, since that is where R1 is
    /// claimed at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void An_empty_catalog_does_not_change_a_single_byte(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        SharedItemDataCatalog.Load("no-such-file.csv");
        Assert.Equal(0, SharedItemDataCatalog.Count);

        var profile = new PlayerProfile(path);
        Assert.SkipWhen(!profile.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(
            profile.ProfileVersion != (int)GameVersion.c_PlayerVersion,
            $"{fileName} is profile version {profile.ProfileVersion}; R1 is only claimed at "
            + $"{(int)GameVersion.c_PlayerVersion}.");

        var original = File.ReadAllBytes(path);

        using var rewritten = TempFile.Create();
        profile.m_filename = rewritten.Path;
        profile.SavePlayerToDisk();

        var diff = ByteDiff.Describe(original, File.ReadAllBytes(rewritten.Path));
        Assert.True(diff is null, $"An unresolvable-item save was altered for {fileName}.\n{diff}");
    }

    /// <summary>
    /// Every item keeps its identity and its values through a full editor
    /// open/save/reopen cycle with no catalog — including the prefab hash,
    /// which is the only identity a 108+ save carries.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Items_survive_an_editor_round_trip_with_no_catalog(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        SharedItemDataCatalog.Load("no-such-file.csv");

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path, overwrite: true);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var before = editor!.View.Inventory.Items;

        // A real edit elsewhere in the file, so this is a genuine save rather
        // than a no-op write — the inventory must survive an unrelated change.
        editor.SetPlayerName(editor.View.Meta.PlayerName + "x");
        editor.Save();

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var after = reopened!.View.Inventory.Items;

        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].PrefabName, after[i].PrefabName);
            Assert.Equal(before[i].Stack, after[i].Stack);
            Assert.Equal(before[i].Quality, after[i].Quality);
            Assert.Equal(before[i].GridX, after[i].GridX);
            Assert.Equal(before[i].GridY, after[i].GridY);
            Assert.Equal(before[i].Durability, after[i].Durability);
        }
    }

    /// <summary>
    /// A catalog-dependent edit aimed at an item the catalog cannot resolve
    /// must do nothing at all, rather than partially apply.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Catalog_dependent_edits_are_inert_without_a_catalog(string? fileName)
    {
        var corpusPath = Corpus.RequireFile(fileName);

        SharedItemDataCatalog.Load("no-such-file.csv");

        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path, overwrite: true);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var target = editor!.View.Inventory.Items.FirstOrDefault();
        Assert.SkipWhen(target is null, $"{fileName} has an empty inventory.");

        editor.FillItemStack(target!.GridX, target.GridY);
        editor.SetItemQuality(target.GridX, target.GridY, 4);
        editor.RepairAllItems();

        var now = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(target.Stack, now.Stack);
        Assert.Equal(target.Quality, now.Quality);
        Assert.Equal(target.Durability, now.Durability);
    }

    /// <summary>
    /// The worst case: neither catalog loads at all. A save still round-trips
    /// byte-for-byte.
    /// </summary>
    /// <remarks>
    /// Both catalogs are shipped data files produced by an external extractor,
    /// so both can be absent, stale or wrong at the same time — a fresh Valheim
    /// release before the extractor has been re-run is exactly that state.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Neither_catalog_loading_does_not_change_a_single_byte(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        SharedItemDataCatalog.Load("no-such-file.csv");
        LocalizationCatalog.Load("no-such-file.csv");
        Assert.Equal(0, SharedItemDataCatalog.Count);

        var profile = new PlayerProfile(path);
        Assert.SkipWhen(!profile.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(
            profile.ProfileVersion != (int)GameVersion.c_PlayerVersion,
            $"{fileName} is profile version {profile.ProfileVersion}; R1 is only claimed at "
            + $"{(int)GameVersion.c_PlayerVersion}.");

        var original = File.ReadAllBytes(path);

        using var rewritten = TempFile.Create();
        profile.m_filename = rewritten.Path;
        profile.SavePlayerToDisk();

        var diff = ByteDiff.Describe(original, File.ReadAllBytes(rewritten.Path));
        Assert.True(diff is null, $"A save was altered with no catalogs loaded, for {fileName}.\n{diff}");
    }

    /// <summary>
    /// Missing localization degrades a displayed name to its raw key — never
    /// to blank, and never to a wrong name.
    /// </summary>
    /// <remarks>
    /// A blank row would be indistinguishable from "nothing recorded here",
    /// which is a different and false claim. The raw key
    /// (<c>$enemy_greydwarf</c>) is ugly but honest, and it is still enough for
    /// a user to tell two rows apart and to search for.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Missing_localization_falls_back_to_raw_keys_not_blanks(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        SharedItemDataCatalog.Load("no-such-file.csv");
        LocalizationCatalog.Load("no-such-file.csv");

        var profile = new PlayerProfile(path);
        Assert.SkipWhen(!profile.Load(), $"{fileName} is outside the compatible profile-version range.");

        var statistics = StatisticsMapper.Map(profile);

        var named = statistics.Slots
            .SelectMany(slot => slot.EnemyStats.SelectMany(list => list)
                .Concat(slot.ItemPickupStats)
                .Concat(slot.ItemCraftStats)
                .Concat(slot.PickableStats)
                .Concat(slot.FoodEatenStats)
                .Concat(slot.PiecesPlacedStats))
            .ToList();

        Assert.SkipWhen(named.Count == 0, $"{fileName} records no name-keyed statistics.");
        Assert.All(named, stat => Assert.False(string.IsNullOrWhiteSpace(stat.Name)));
    }

    /// <summary>
    /// The same value is recorded whether or not the catalogs resolved — only
    /// the label changes.
    /// </summary>
    /// <remarks>
    /// The point of the whole degradation story: catalogs are a presentation
    /// concern. If a missing catalog could change a number, it would have
    /// escaped presentation and become a data concern.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Catalog_state_never_changes_a_recorded_value(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var profile = new PlayerProfile(path);
        Assert.SkipWhen(!profile.Load(), $"{fileName} is outside the compatible profile-version range.");

        LocalizationCatalog.Load(TestPaths.LocalizationDataCsvPath);
        SharedItemDataCatalog.Load(TestPaths.SharedItemDataCsvPath);
        var withCatalogs = Values(StatisticsMapper.Map(profile));

        LocalizationCatalog.Load("no-such-file.csv");
        SharedItemDataCatalog.Load("no-such-file.csv");
        var without = Values(StatisticsMapper.Map(profile));

        Assert.Equal(withCatalogs, without);
    }

    private static List<float> Values(StatisticsDto statistics)
        => statistics.Slots
            .SelectMany(slot => slot.PlayerStats
                .Concat(slot.EnemyStats.SelectMany(list => list))
                .Concat(slot.ItemPickupStats)
                .Concat(slot.ItemCraftStats)
                .Concat(slot.PickableStats)
                .Concat(slot.FoodEatenStats)
                .Concat(slot.PiecesPlacedStats))
            .Select(stat => stat.Value)
            .ToList();
}
