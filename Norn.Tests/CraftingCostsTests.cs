using Norn.Adapter;

namespace Norn.Tests;

/// <summary>
/// <see cref="CraftingCosts"/> against synthetic recipes — pure math, no catalog.
/// </summary>
public class CraftingCostsTests
{
    private static readonly RecipeResourceDto Hide = new("Hide", 5, 2, Upgrader: false);
    private static readonly RecipeResourceDto Paw = new("Paw", 2, 0, Upgrader: false);
    private static readonly RecipeResourceDto Berries = new("Berries", 4, 1, Upgrader: false);
    private static readonly RecipeResourceDto Idol = new("Idol", 1, 0, Upgrader: true);

    private static RecipeDto Recipe(bool craftable = true, params RecipeResourceDto[] resources) =>
        new("Recipe_Test", "Test", 1, "forge", "$piece_forge", 1, craftable, false, null,
            resources.Length > 0 ? resources : [Hide, Paw, Berries, Idol]);

    // Game formula: q-1 up to q=3, then 4 + (q-4)/2, floored — including the jump at q=4.
    [Theory]
    [InlineData(1, 5, 2, 5)]
    [InlineData(2, 5, 2, 2)]
    [InlineData(3, 5, 2, 4)]
    [InlineData(4, 5, 2, 8)]
    [InlineData(5, 5, 2, 9)]
    [InlineData(6, 5, 2, 10)]
    [InlineData(5, 5, 3, 13)]
    [InlineData(3, 5, 0, 0)]
    public void AmountFor_follows_the_game_formula(int quality, int amount, int perLevel, int expected)
    {
        Assert.Equal(expected, CraftingCosts.AmountFor(new RecipeResourceDto("X", amount, perLevel, false), quality));
    }

    [Fact]
    public void Materials_for_the_craft_are_the_base_amounts_without_upgrader_resources()
    {
        var materials = CraftingCosts.Materials(Recipe(), targetLevel: 1, upgradeOnly: false, times: 1);

        Assert.Equal([new("Hide", 5), new("Paw", 2), new("Berries", 4)], materials);
    }

    [Fact]
    public void Materials_up_to_a_level_sum_the_craft_and_every_upgrade_step()
    {
        // Craft (5, 2, 4) + level 2 (2, 0, 1) + level 3 (4, 0, 2)
        var materials = CraftingCosts.Materials(Recipe(), targetLevel: 3, upgradeOnly: false, times: 1);

        Assert.Equal([new("Hide", 11), new("Paw", 2), new("Berries", 7)], materials);
    }

    [Fact]
    public void Upgrade_only_is_the_single_step_to_that_level_with_zero_amounts_dropped()
    {
        var materials = CraftingCosts.Materials(Recipe(), targetLevel: 3, upgradeOnly: true, times: 1);

        Assert.Equal([new("Hide", 4), new("Berries", 2)], materials);
    }

    [Fact]
    public void An_upgrade_only_recipe_starts_at_level_2()
    {
        var recipe = Recipe(craftable: false);

        Assert.Equal(2, CraftingCosts.FirstLevel(recipe));
        Assert.Empty(CraftingCosts.Materials(recipe, targetLevel: 1, upgradeOnly: false, times: 1));
        // Level 2 (2, 0, 1) + level 3 (4, 0, 2) — no craft step
        Assert.Equal([new("Hide", 6), new("Berries", 3)], CraftingCosts.Materials(recipe, targetLevel: 3, upgradeOnly: false, times: 1));
    }

    [Fact]
    public void Times_multiplies_every_amount()
    {
        var materials = CraftingCosts.Materials(Recipe(), targetLevel: 1, upgradeOnly: false, times: 3);

        Assert.Equal([new("Hide", 15), new("Paw", 6), new("Berries", 12)], materials);
    }

    [Fact]
    public void A_piece_costs_its_plain_amounts_times_n_merged_by_item()
    {
        var piece = new PieceDto("portal_wood", "$piece_portal", "Hammer", PieceCategory.Misc, "piece_workbench", "$piece_workbench", null,
            [new("GreydwarfEye", 10), new("FineWood", 20), new("GreydwarfEye", 2)]);

        Assert.Equal([new("GreydwarfEye", 24), new("FineWood", 40)], CraftingCosts.Materials(piece, times: 2));
    }

    [Fact]
    public void Requirements_naming_the_same_item_are_merged()
    {
        var recipe = Recipe(true, new RecipeResourceDto("Wood", 2, 0, false), new RecipeResourceDto("Stone", 1, 0, false), new RecipeResourceDto("Wood", 3, 0, false));

        Assert.Equal([new("Wood", 5), new("Stone", 1)], CraftingCosts.Materials(recipe, 1, false, 1));
    }
}
