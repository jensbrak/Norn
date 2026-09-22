using Norn.Adapter;

namespace Norn.Tests;

/// <summary>
/// Groups every test class that calls <c>Load</c> on either
/// <see cref="SharedItemDataCatalog"/> or <see cref="LocalizationCatalog"/>
/// (this class, <see cref="LocalizationCatalogTests"/>,
/// <see cref="CharacterEditorTests"/>' inventory tests, and
/// <see cref="AdapterMappingTests"/>) so xUnit runs them sequentially rather
/// than in parallel. Both catalogs are shared statics — matching Loki's own
/// <c>ItemDb</c> precedent for the item
/// one — and xUnit's default cross-*collection* parallelism would let one
/// test's <c>Load</c> call stomp on another's expected state mid-run
/// regardless of which of the two catalogs each side happens to touch; one
/// shared collection for both is simpler and safer than two collections
/// that would still race against each other.
/// </summary>
[CollectionDefinition(Name)]
public class CatalogCollection
{
    public const string Name = "Catalog";
}

/// <summary>
/// Exercises <see cref="SharedItemDataCatalog"/>'s parsing and precedence
/// logic against synthetic CSVs (deterministic, independent of the real
/// data), plus a couple of spot-checks against the real, tracked
/// `Norn.UI/Content/SharedItemData.csv` — still guarded with
/// <c>Assert.SkipWhen</c> for robustness (e.g. a standalone publish output
/// with no source tree alongside it), not because absence is expected.
/// </summary>
[Collection(CatalogCollection.Name)]
public class SharedItemDataCatalogTests
{
    private const string Header =
        "ItemName,IsTeleportable,UsesDurability,MaxDurability,DurabilityPerLevel,MaxStack,DisplayName,MaxQuality,ItemType\n";

    [Fact]
    public void Resolves_a_known_item_from_a_synthetic_csv()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header
            + "Wood,True,False,100,50,50,Wood,1,1\n"
            + "SwordIron,True,True,150,20,1,Iron Sword,4,3\n");

        SharedItemDataCatalog.Load(temp.Path);

        var wood = SharedItemDataCatalog.TryFind("Wood");
        Assert.NotNull(wood);
        Assert.Equal("Wood", wood!.DisplayName);
        Assert.False(wood.UsesDurability);
        Assert.Equal(50, wood.MaxStack);

        // No Weight/ScaleWeightByQuality columns in this 9-field row — an
        // external-override CSV predating the trailing weight columns must
        // still parse, defaulted rather
        // than rejected (SharedItemDataCatalog.ParseRow).
        Assert.Equal(0, wood.Weight);
        Assert.Equal(0, wood.ScaleWeightByQuality);

        var sword = SharedItemDataCatalog.TryFind("SwordIron");
        Assert.NotNull(sword);
        Assert.True(sword!.UsesDurability);
        Assert.Equal(4, sword.MaxQuality);
        Assert.Equal(150 + 3 * 20, sword.MaxDurabilityFor(4));
    }

    [Fact]
    public void QualityOverMax_is_zero_at_or_under_max_and_the_delta_above_it()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header + "SwordIron,True,True,150,20,1,Iron Sword,4,3\n");

        SharedItemDataCatalog.Load(temp.Path);

        var sword = SharedItemDataCatalog.TryFind("SwordIron");
        Assert.NotNull(sword);
        Assert.Equal(0, sword!.QualityOverMax(1));
        Assert.Equal(0, sword.QualityOverMax(4));
        Assert.Equal(1, sword.QualityOverMax(5));
        Assert.Equal(38, sword.QualityOverMax(42));
    }

    [Fact]
    public void Parses_weight_columns_and_computes_the_quality_scaled_formula()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header.Replace("ItemType\n", "ItemType,Weight,ScaleWeightByQuality\n")
            + "Coins,True,False,100,50,999,Coins,1,1,0.1,0\n"
            + "Fish8,True,False,100,50,10,Fish8,5,21,2,1\n");

        SharedItemDataCatalog.Load(temp.Path);

        var coins = SharedItemDataCatalog.TryFind("Coins");
        Assert.NotNull(coins);
        Assert.Equal(0.1, coins!.Weight);
        Assert.Equal(0, coins.ScaleWeightByQuality);
        // Zero scale factor: quality never changes the effective weight
        // (the game's own guard, reproduced
        // here as an arithmetic identity rather than an explicit branch).
        Assert.Equal(0.1, coins.WeightFor(1));
        Assert.Equal(0.1, coins.WeightFor(5));

        var fish = SharedItemDataCatalog.TryFind("Fish8");
        Assert.NotNull(fish);
        // Real game values: quality 1 stays
        // at the base weight, quality 5 is 2 * (1 + 4*1) = 10.
        Assert.Equal(2, fish!.WeightFor(1));
        Assert.Equal(10, fish.WeightFor(5));
    }

    [Fact]
    public void Handles_a_quoted_field_with_an_embedded_comma_and_trailing_space()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header
            + "TrinketExample,True,False,100,50,1,\"Ring, of Power \",1,24\n");

        SharedItemDataCatalog.Load(temp.Path);

        var item = SharedItemDataCatalog.TryFind("TrinketExample");
        Assert.NotNull(item);
        Assert.Equal("Ring, of Power ", item!.DisplayName);
    }

    [Fact]
    public void Unresolved_item_returns_null_not_a_default()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header + "Wood,True,False,100,50,50,Wood,1,1\n");

        SharedItemDataCatalog.Load(temp.Path);

        Assert.Null(SharedItemDataCatalog.TryFind("SomeItemNotInTheCatalog"));
    }

    [Fact]
    public void Loads_the_first_existing_candidate_and_ignores_later_ones()
    {
        using var first = TempFile.Create();
        using var second = TempFile.Create();
        File.WriteAllText(first.Path, Header + "FromFirst,True,False,100,50,50,From First,1,1\n");
        File.WriteAllText(second.Path, Header + "FromSecond,True,False,100,50,50,From Second,1,1\n");
        var missing = TempFile.NonExistentPath(".csv");

        SharedItemDataCatalog.Load(missing, first.Path, second.Path);

        Assert.NotNull(SharedItemDataCatalog.TryFind("FromFirst"));
        Assert.Null(SharedItemDataCatalog.TryFind("FromSecond"));
    }

    [Fact]
    public void Missing_and_corrupt_candidates_degrade_to_an_empty_catalog()
    {
        using var corrupt = TempFile.Create();
        File.WriteAllText(corrupt.Path, Header + "Broken,not-a-bool,False,100,50,50,Broken,1,1\n");
        var missing = TempFile.NonExistentPath(".csv");

        SharedItemDataCatalog.Load(missing, corrupt.Path);

        Assert.Equal(0, SharedItemDataCatalog.Count);
        Assert.Null(SharedItemDataCatalog.TryFind("Broken"));
    }

    [Fact]
    public void AllOfType_returns_only_matching_entries()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header
            + "Wood,True,False,100,50,50,Wood,1,1\n"
            + "Beard1,True,False,100,50,1,Majestic,1,10\n"
            + "Hair1,True,False,100,50,1,Windswept,1,10\n");

        SharedItemDataCatalog.Load(temp.Path);

        var customization = SharedItemDataCatalog.AllOfType(ItemType.Customization).ToList();
        Assert.Equal(2, customization.Count);
        Assert.Contains(customization, i => i.ItemName == "Beard1");
        Assert.Contains(customization, i => i.ItemName == "Hair1");
        Assert.DoesNotContain(customization, i => i.ItemName == "Wood");
    }

    [Fact]
    public void Resolves_known_items_from_the_real_development_csv()
    {
        var path = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(path is null, "Norn.UI/Content/SharedItemData.csv not present.");

        SharedItemDataCatalog.Load(path);

        var amber = SharedItemDataCatalog.TryFind("Amber");
        Assert.NotNull(amber);
        Assert.Equal("Amber", amber!.DisplayName);
        Assert.Equal(20, amber.MaxStack);

        // Real prefab-verified value.
        var wood = SharedItemDataCatalog.TryFind("Wood");
        Assert.NotNull(wood);
        Assert.Equal(2, wood!.Weight);

        // Non-ASCII display names survive the read.
        var trinket = SharedItemDataCatalog.TryFind("TrinketFlametalEitr");
        Assert.NotNull(trinket);
        Assert.Equal("Jörmundling", trinket!.DisplayName);

        // This used to additionally assert a trailing space, because the
        // 0.221.10-era export quoted exactly one field ("Jörmundling ") and
        // that was the only real-data coverage of quote handling. The 1.0.7
        // export has no quoted fields at all, so that coverage now lives
        // entirely in the synthetic
        // Handles_a_quoted_field_with_an_embedded_comma_and_trailing_space
        // test above — which is the better home for it anyway, since it does
        // not depend on a quirk of whichever export happens to be checked in.
    }
}
