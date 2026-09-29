using Norn.Adapter;

namespace Norn.Tests;

/// <summary>
/// <see cref="RecipeCatalog"/>'s parsing and fallback against synthetic JSON,
/// plus the bundled file's consistency with the bundled item catalog.
/// </summary>
[Collection(CatalogCollection.Name)]
public class RecipeCatalogTests
{
    private const string OneRecipe = """
        {
          "recipes": [
            {
              "name": "Recipe_Bronze5",
              "item": "Bronze",
              "amount": 5,
              "station": "forge",
              "stationToken": "$piece_forge",
              "minStationLevel": 1,
              "craftable": true,
              "requireOnlyOneIngredient": false,
              "season": null,
              "resources": [
                { "item": "Copper", "amount": 10, "amountPerLevel": 1, "upgrader": false },
                { "item": "Tin", "amount": 5, "amountPerLevel": 1, "upgrader": false }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Loads_a_synthetic_file()
    {
        using var temp = TempFile.Create(".json");
        File.WriteAllText(temp.Path, OneRecipe);

        RecipeCatalog.Load(temp.Path);

        var recipe = Assert.Single(RecipeCatalog.All);
        Assert.Equal("Recipe_Bronze5", recipe.Name);
        Assert.Equal("Bronze", recipe.ItemName);
        Assert.Equal(5, recipe.Amount);
        Assert.Equal("$piece_forge", recipe.StationToken);
        Assert.Equal([new RecipeResourceDto("Copper", 10, 1, false), new RecipeResourceDto("Tin", 5, 1, false)], recipe.Resources);
    }

    [Fact]
    public void Falls_through_a_missing_or_corrupt_candidate_to_the_next()
    {
        using var corrupt = TempFile.Create(".json");
        using var good = TempFile.Create(".json");
        File.WriteAllText(corrupt.Path, "{ \"recipes\": [ { \"name\": \"Recipe_X\" } ] }");
        File.WriteAllText(good.Path, OneRecipe);

        RecipeCatalog.Load(null, Path.Combine(Path.GetTempPath(), "no-such-recipes.json"), corrupt.Path, good.Path);

        Assert.Equal(1, RecipeCatalog.Count);
    }

    [Fact]
    public void Is_empty_when_no_candidate_loads()
    {
        using var corrupt = TempFile.Create(".json");
        File.WriteAllText(corrupt.Path, "not json");

        RecipeCatalog.Load(corrupt.Path);

        Assert.Equal(0, RecipeCatalog.Count);
    }

    /// <summary>Norn names every recipe item through the item catalog, so the
    /// two bundled files must agree — Vade checks this at extraction; this
    /// guards against the two being refreshed out of step.</summary>
    [Fact]
    public void Every_bundled_recipe_item_resolves_in_the_bundled_item_catalog()
    {
        var jsonPath = TestPaths.RecipeDataJsonPath;
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(jsonPath is null || csvPath is null, "Bundled Norn.UI/Content files not present.");
        RecipeCatalog.Load(jsonPath);
        SharedItemDataCatalog.Load(csvPath);

        Assert.True(RecipeCatalog.Count >= 300, $"Only {RecipeCatalog.Count} recipes loaded.");
        var missing = RecipeCatalog.All
            .SelectMany(r => r.Resources.Select(q => q.ItemName).Prepend(r.ItemName))
            .Where(name => SharedItemDataCatalog.TryFind(name) is null)
            .Distinct()
            .ToList();
        Assert.Empty(missing);
    }
}
