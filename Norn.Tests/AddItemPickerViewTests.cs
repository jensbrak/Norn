using Norn.Adapter;
using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="AddItemPickerView"/>'s pure filter/sort pipeline.
/// </summary>
/// <remarks>
/// The fixtures deliberately make prefab name and display name disagree
/// (found in review). Previously every item's display name contained the
/// same word as its prefab name, and both fields sorted into the same
/// order — so filtering or sorting by the wrong field produced identical
/// results, and neither the search test's own "not prefab name" claim nor
/// the ordering test could distinguish a correct implementation from one
/// keyed on the other field. <see cref="Shield"/> carries the search term
/// only in its prefab name and <see cref="Helmet"/> only in its display
/// name, so the two fields now yield genuinely different sets.
/// </remarks>
public class AddItemPickerViewTests
{
    private static SharedItemDataDto Item(string name, string displayName, ItemType type) =>
        new(name, IsTeleportable: true, UsesDurability: false, MaxDurability: 0, DurabilityPerLevel: 0,
            MaxStack: 1, DisplayName: displayName, MaxQuality: 1, ItemType: type, Weight: 0, ScaleWeightByQuality: 0,
            CanHaveCrafterTag: true);

    private static readonly SharedItemDataDto Sword = Item("SwordIron", "Iron Sword", ItemType.OneHandedWeapon);
    private static readonly SharedItemDataDto Axe = Item("AxeIron", "Iron Axe", ItemType.Tool);
    private static readonly SharedItemDataDto Wood = Item("Wood", "Wood", ItemType.Material);

    /// <summary>"Iron" in the prefab name only — a wrong-field search would
    /// wrongly include this.</summary>
    private static readonly SharedItemDataDto Shield = Item("ShieldIron", "Banded Shield", ItemType.Shield);

    /// <summary>"Iron" in the display name only — a wrong-field search would
    /// wrongly exclude this.</summary>
    private static readonly SharedItemDataDto Helmet = Item("HelmetDrake", "Drake Iron Helmet", ItemType.Helmet);

    private static readonly IReadOnlyList<SharedItemDataDto> All = [Sword, Axe, Wood, Shield, Helmet];

    [Fact]
    public void Null_category_matches_everything()
    {
        var visible = AddItemPickerView.Apply(All, "", category: null);

        Assert.Equal(5, visible.Count);
    }

    [Fact]
    public void Category_filters_to_matching_ItemType_only()
    {
        var visible = AddItemPickerView.Apply(All, "", ItemType.Tool);

        Assert.Equal([Axe], visible.Select(row => row.Item));
    }

    [Fact]
    public void Search_filters_case_insensitively_by_display_name_not_prefab_name()
    {
        var visible = AddItemPickerView.Apply(All, "iron", category: null);

        Assert.Equal(3, visible.Count);
        Assert.Contains(Sword, visible.Select(row => row.Item));
        Assert.Contains(Axe, visible.Select(row => row.Item));

        // Display name carries "Iron", prefab name doesn't — a prefab-keyed
        // search would miss it.
        Assert.Contains(Helmet, visible.Select(row => row.Item));

        // Prefab name carries "Iron", display name doesn't — a prefab-keyed
        // search would wrongly include it.
        Assert.DoesNotContain(Shield, visible.Select(row => row.Item));
    }

    [Fact]
    public void Category_and_search_combine()
    {
        var visible = AddItemPickerView.Apply(All, "iron", ItemType.Tool);

        Assert.Equal([Axe], visible.Select(row => row.Item));
    }

    [Fact]
    public void Results_are_ordered_alphabetically_by_display_name()
    {
        var visible = AddItemPickerView.Apply(All, "", category: null);

        // Display order (Banded Shield, Drake Iron Helmet, Iron Axe, Iron
        // Sword, Wood) differs from prefab order (AxeIron, HelmetDrake,
        // ShieldIron, SwordIron, Wood), so sorting by the wrong field fails
        // here rather than coinciding.
        Assert.Equal([Shield, Helmet, Axe, Sword, Wood], visible.Select(row => row.Item));
    }

    [Fact]
    public void Empty_search_text_matches_everything()
    {
        var visible = AddItemPickerView.Apply(All, "   ", category: null);

        Assert.Equal(5, visible.Count);
    }

    [Fact]
    public void No_match_returns_an_empty_list()
    {
        var visible = AddItemPickerView.Apply(All, "nonexistent", category: null);

        Assert.Empty(visible);
    }

    // Valheim 1.0 made shared display names common: troll leather armour exists as the
    // craftable item, a FallenWarrior variant, and a third registered variant, all under
    // one name. These three fixtures are that shape.
    private static readonly SharedItemDataDto Troll = Item("ArmorTrollLeatherChest", "Troll Leather Chest", ItemType.Chest);
    private static readonly SharedItemDataDto TrollFallen = Item("FW_ArmorTrollLeatherChest", "Troll Leather Chest", ItemType.Chest);
    private static readonly SharedItemDataDto TrollThird = Item("SP_ArmorTrollLeatherChest", "Troll Leather Chest", ItemType.Chest);

    private static readonly IReadOnlyList<SharedItemDataDto> WithDuplicates =
        [Sword, Axe, Wood, Shield, Helmet, Troll, TrollFallen, TrollThird];

    [Fact]
    public void An_unambiguous_display_name_is_shown_as_is()
    {
        var visible = AddItemPickerView.Apply(WithDuplicates, "", category: null);

        Assert.Equal("Iron Sword", visible.Single(row => row.Item == Sword).Label);
    }

    [Fact]
    public void Items_sharing_a_display_name_are_all_kept_and_told_apart_by_prefab()
    {
        var visible = AddItemPickerView.Apply(WithDuplicates, "troll", category: null);

        // All three stay. Each writes a different prefab hash into the save, and which one
        // a user wants is not knowable here — hiding one would make it unaddable.
        Assert.Equal(3, visible.Count);
        Assert.Equal(
            ["Troll Leather Chest (ArmorTrollLeatherChest)",
             "Troll Leather Chest (FW_ArmorTrollLeatherChest)",
             "Troll Leather Chest (SP_ArmorTrollLeatherChest)"],
            visible.Select(row => row.Label).Order());
    }

    [Fact]
    public void The_qualifier_disappears_once_the_filter_leaves_only_one_of_them()
    {
        // Ambiguity is judged against the visible list, not the whole catalog: filtering
        // to a single survivor makes the prefab qualifier pointless noise.
        var narrowed = AddItemPickerView.Apply([Sword, Troll], "troll", category: null);

        Assert.Equal("Troll Leather Chest", Assert.Single(narrowed).Label);
    }

    [Fact]
    public void Duplicates_do_not_disturb_unrelated_rows()
    {
        var visible = AddItemPickerView.Apply(WithDuplicates, "", category: null);

        Assert.Equal(8, visible.Count);
        Assert.All(
            visible.Where(row => row.Item.DisplayName != "Troll Leather Chest"),
            row => Assert.Equal(row.Item.DisplayName, row.Label));
    }

    /// <summary>
    /// Items the game ships without a translation keep their raw token as a display name.
    /// '$' sorts ahead of every letter, so without care they lead the list.
    /// </summary>
    [Fact]
    public void Unresolved_token_names_sort_last_not_first()
    {
        var unresolved = Item("TrophyDeerWhite", "$item_trophy_deer_white", ItemType.Trophy);

        var visible = AddItemPickerView.Apply([unresolved, Sword, Axe], "", category: null);

        Assert.Equal(["Iron Axe", "Iron Sword", "$item_trophy_deer_white"], visible.Select(row => row.Label));
    }
}
