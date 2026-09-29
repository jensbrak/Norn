using Norn.Adapter;
using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="AddMaterialsPickerView"/>'s pure filter/sort/label pipeline.
/// </summary>
public class AddMaterialsPickerViewTests
{
    private static SharedItemDataDto Item(string name, string displayName, ItemType type) =>
        new(name, IsTeleportable: true, UsesDurability: false, MaxDurability: 0, DurabilityPerLevel: 0,
            MaxStack: 50, DisplayName: displayName, MaxQuality: 1, ItemType: type, Weight: 0, ScaleWeightByQuality: 0,
            CanHaveCrafterTag: true);

    private static RecipeDto Recipe(string name, string itemName, int amount = 1, bool onlyOne = false) =>
        new(name, itemName, amount, "forge", "$piece_forge", 1, Craftable: true, RequireOnlyOneIngredient: onlyOne, Season: null,
            [new RecipeResourceDto("Wood", 1, 0, false)]);

    private static PieceDto Piece(string name, string token, string tool = "Hammer", PieceCategory category = PieceCategory.Misc) =>
        new(name, token, tool, category, null, null, null, [new ItemAmount("Wood", 1)]);

    private static readonly Dictionary<string, SharedItemDataDto> Items = new()
    {
        ["SwordIron"] = Item("SwordIron", "Iron Sword", ItemType.OneHandedWeapon),
        ["Bronze"] = Item("Bronze", "Bronze", ItemType.Material),
        ["ArrowWood"] = Item("ArrowWood", "Wood Arrow", ItemType.Ammo),
        ["FishRaw"] = Item("FishRaw", "Raw fish", ItemType.Material),
        ["Untranslated"] = Item("Untranslated", "$item_untranslated", ItemType.Material),
        ["TrollChestA"] = Item("TrollChestA", "Troll leather tunic", ItemType.Chest),
        ["TrollChestB"] = Item("TrollChestB", "Troll leather tunic", ItemType.Chest),
    };

    private static readonly Dictionary<string, string> Texts = new()
    {
        ["$piece_portal"] = "Portal",
        ["$piece_portal_stone"] = "Stone Portal",
        ["$piece_raise"] = "Raise Ground",
        ["$piece_workbench"] = "Workbench",
    };

    private static readonly IReadOnlyList<RecipeDto> Recipes =
    [
        Recipe("Recipe_SwordIron", "SwordIron"),
        Recipe("Recipe_Bronze", "Bronze"),
        Recipe("Recipe_Bronze5", "Bronze", amount: 5),
        Recipe("Recipe_ArrowWood", "ArrowWood", amount: 20),
        Recipe("Recipe_Fish1", "FishRaw", onlyOne: true),
        Recipe("Recipe_Untranslated", "Untranslated"),
        Recipe("Recipe_Unknown", "NotInCatalog"),
        Recipe("Recipe_TrollChestA", "TrollChestA"),
        Recipe("Recipe_TrollChestB", "TrollChestB"),
    ];

    private static readonly IReadOnlyList<PieceDto> Pieces =
    [
        Piece("portal_wood", "$piece_portal"),
        Piece("portal_stone", "$piece_portal_stone"),
        Piece("piece_workbench", "$piece_workbench", category: PieceCategory.Crafting),
        Piece("raise_v2", "$piece_raise", tool: "Hoe"),
        Piece("piece_untranslated", "$piece_untranslated"),
    ];

    private static IReadOnlyList<MaterialsRow> Apply(string search = "", MaterialsFilter filter = default) =>
        AddMaterialsPickerView.Apply(Recipes, Pieces, name => Items.GetValueOrDefault(name), token => Texts.GetValueOrDefault(token), search, filter);

    private static List<string> Labels(IEnumerable<MaterialsRow> rows) => [.. rows.Select(row => row.Label)];

    [Fact]
    public void All_holds_both_kinds_minus_unresolved_outputs_and_one_ingredient_recipes()
    {
        var rows = Apply();

        Assert.Equal(7, rows.OfType<RecipeRow>().Count());
        Assert.Equal(5, rows.OfType<PieceRow>().Count());
        Assert.DoesNotContain(rows.OfType<RecipeRow>(), row => row.Recipe.Name is "Recipe_Unknown" or "Recipe_Fish1");
    }

    [Fact]
    public void Recipe_labels_show_the_output_amount_and_pieces_their_localized_name()
    {
        var labels = Labels(Apply());

        Assert.Contains("Bronze", labels);
        Assert.Contains("Bronze ×5", labels);
        Assert.Contains("Wood Arrow ×20", labels);
        Assert.Contains("Portal", labels);
        Assert.Contains("$piece_untranslated", labels); // falls back to its token
    }

    [Fact]
    public void Kind_filters_narrow_to_one_kind_and_further_to_a_category_or_group()
    {
        Assert.All(Apply(filter: new(MaterialsKind.Recipe)), row => Assert.IsType<RecipeRow>(row));
        Assert.All(Apply(filter: new(MaterialsKind.Piece)), row => Assert.IsType<PieceRow>(row));
        Assert.Equal(["Iron Sword"], Labels(Apply(filter: new(MaterialsKind.Recipe, ItemType: ItemType.OneHandedWeapon))));
        Assert.Equal(["Workbench"], Labels(Apply(filter: new(MaterialsKind.Piece, PieceGroup: "Crafting"))));
        Assert.Equal(["Raise Ground"], Labels(Apply(filter: new(MaterialsKind.Piece, PieceGroup: "Hoe"))));
    }

    [Fact]
    public void Hammer_pieces_group_by_category_and_other_tools_by_tool()
    {
        Assert.Equal("Crafting", AddMaterialsPickerView.PieceGroup(Pieces[2]));
        Assert.Equal("Hoe", AddMaterialsPickerView.PieceGroup(Pieces[3]));
    }

    [Fact]
    public void Search_spans_both_kinds_by_shown_name()
    {
        Assert.Equal(["Portal", "Stone Portal"], Labels(Apply(search: "portal")));
        Assert.Equal(["Wood Arrow ×20"], Labels(Apply(search: "arrow")));
    }

    [Fact]
    public void Identical_labels_are_qualified_with_the_underlying_name_only_where_needed()
    {
        var labels = Labels(Apply());
        Assert.Contains("Troll leather tunic (Recipe_TrollChestA)", labels);
        Assert.Contains("Troll leather tunic (Recipe_TrollChestB)", labels);

        // A visible list holding only one of the pair needs no qualifier.
        var single = AddMaterialsPickerView.Apply([Recipes[^1]], [], name => Items.GetValueOrDefault(name), _ => null, "", default);
        Assert.Equal("Troll leather tunic", Assert.Single(single).Label);
    }

    [Fact]
    public void Sorts_by_label_with_unresolved_names_last()
    {
        var labels = Labels(Apply());

        Assert.Equal("Bronze", labels[0]);
        Assert.StartsWith("$", labels[^1]);
        Assert.StartsWith("$", labels[^2]);
    }
}
