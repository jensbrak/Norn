using Norn.Adapter;
using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// <see cref="CarryWeight"/> and <see cref="CarryWeightText"/> against a
/// synthetic catalog — pure math and formatting, no bundled data.
/// </summary>
public class CarryWeightTests
{
    private static readonly Dictionary<string, SharedItemDataDto> Catalog = new[]
    {
        Shared("Wood", weight: 2, maxStack: 50),
        Shared("Coins", weight: 0.1, maxStack: 999),
        Shared("Fish", weight: 2, maxStack: 20, scaleByQuality: 1),
        Shared("BeltStrength", weight: 2, maxStack: 1),
    }.ToDictionary(item => item.ItemName);

    private static SharedItemDataDto Shared(string name, double weight, int maxStack, double scaleByQuality = 0) =>
        new(name, IsTeleportable: true, UsesDurability: false, MaxDurability: 0, DurabilityPerLevel: 0,
            MaxStack: maxStack, DisplayName: name, MaxQuality: 5, ItemType: ItemType.Material, Weight: weight,
            ScaleWeightByQuality: scaleByQuality, CanHaveCrafterTag: false);

    private static SharedItemDataDto? Find(string name) => Catalog.GetValueOrDefault(name);

    private static ItemDto Item(string prefab, int stack = 1, int quality = 1, bool equipped = false) =>
        new(prefab, stack, Durability: 0, GridX: 0, GridY: 0, equipped, quality, Variant: 0, CrafterId: 0,
            CrafterName: "", CustomData: [], WorldLevel: 0, PickedUp: true, Cheated: false);

    [Fact]
    public void Load_sums_every_stack_equipped_or_not()
    {
        var load = CarryWeight.LoadOf([Item("Wood", stack: 10), Item("BeltStrength", equipped: true)], Find);

        Assert.Equal(new CarryLoad(22, 0), load);
    }

    [Fact]
    public void Load_scales_by_quality_where_the_item_does()
    {
        // Fish: 2 × (1 + (3 − 1) × 1) = 6 each. Wood has no scale factor.
        var load = CarryWeight.LoadOf([Item("Fish", stack: 2, quality: 3), Item("Wood", quality: 3)], Find);

        Assert.Equal(14, load.Weight);
    }

    [Fact]
    public void An_item_the_catalog_cannot_resolve_weighs_nothing_and_is_counted_as_unknown()
    {
        var load = CarryWeight.LoadOf([Item("Wood"), Item("", stack: 30), Item("Mystery")], Find);

        Assert.Equal(new CarryLoad(2, 2), load);
    }

    [Fact]
    public void Added_items_are_weighed_at_quality_1()
    {
        var load = CarryWeight.LoadOf([new ItemAmount("Fish", 3), new ItemAmount("Mystery", 1)], Find);

        Assert.Equal(new CarryLoad(6, 1), load);
    }

    [Fact]
    public void Loads_add_up()
    {
        Assert.Equal(new CarryLoad(5, 3), new CarryLoad(2, 1) + new CarryLoad(3, 2));
    }

    [Fact]
    public void Capacity_is_the_base_plus_megingjord_only_while_equipped()
    {
        Assert.Equal(300, CarryWeight.CapacityOf([Item("Wood")]));
        Assert.Equal(300, CarryWeight.CapacityOf([Item("BeltStrength")]));
        Assert.Equal(450, CarryWeight.CapacityOf([Item("BeltStrength", equipped: true)]));
        Assert.Equal(300, CarryWeight.CapacityOf([Item("Wood", equipped: true)]));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.1, 1)]
    [InlineData(299.5, 300)]
    [InlineData(300, 300)]
    [InlineData(300.001, 301)]
    public void Shown_rounds_up_like_the_game(double weight, int expected)
    {
        Assert.Equal(expected, CarryWeight.Shown(weight));
    }

    [Fact]
    public void Shown_ignores_floating_point_noise_above_a_whole_number()
    {
        // Thirty stacks of 11 coins sum to 33.000000000000014 in binary, which
        // a bare round-up would show as 34.
        var load = CarryWeight.LoadOf(Enumerable.Repeat(Item("Coins", stack: 11), 30), Find);

        Assert.NotEqual(33, load.Weight);
        Assert.Equal(33, CarryWeight.Shown(load.Weight));
    }

    [Fact]
    public void Over_compares_the_shown_numbers_and_exactly_at_capacity_is_not_over()
    {
        Assert.False(CarryWeight.IsOver(300, 300));
        Assert.False(CarryWeight.IsOver(299.2, 300));
        Assert.True(CarryWeight.IsOver(300.5, 300));
    }

    [Fact]
    public void Total_reads_like_the_game_and_marks_unknowns()
    {
        Assert.Equal("213/450", CarryWeightText.Total(new CarryLoad(212.4, 0), 450));
        Assert.Equal("213/300 (+?)", CarryWeightText.Total(new CarryLoad(212.4, 2), 300));
    }

    [Fact]
    public void Addition_shows_the_added_weight_and_the_resulting_total()
    {
        var text = CarryWeightText.Addition(new CarryLoad(0.5, 0), new CarryLoad(212.4, 0), 300);

        Assert.Equal($"+{0.5:0.0} → 213/300", text);
    }

    [Fact]
    public void Addition_carries_unknowns_from_either_side()
    {
        Assert.EndsWith("(+?)", CarryWeightText.Addition(new CarryLoad(1, 1), new CarryLoad(1, 0), 300));
        Assert.EndsWith("(+?)", CarryWeightText.Addition(new CarryLoad(1, 0), new CarryLoad(1, 1), 300));
    }

    [Fact]
    public void Explain_names_megingjord_and_unknowns_only_when_they_apply()
    {
        var plain = CarryWeightText.Explain(new CarryLoad(10, 0), megingjordEquipped: false);
        var full = CarryWeightText.Explain(new CarryLoad(10, 1), megingjordEquipped: true);

        Assert.DoesNotContain("Megingjord", plain);
        Assert.DoesNotContain("unknown", plain);
        Assert.Contains("300 base + 150 Megingjord", full);
        Assert.Contains("1 item of unknown weight", full);
    }
}
