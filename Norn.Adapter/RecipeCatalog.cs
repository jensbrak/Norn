using System.Text.Json;

namespace Norn.Adapter;

/// <summary>
/// Norn's sole seam onto the recipe data (<c>RecipeData.json</c>, written by
/// <c>Tools/Vade</c>). Same static, load-once shape as
/// <see cref="SharedItemDataCatalog"/>: the caller passes candidate paths in
/// precedence order, and a missing or unparseable file leaves the catalog
/// empty rather than failing — recipe-based adds degrade, the app doesn't.
/// </summary>
public static class RecipeCatalog
{
    private static IReadOnlyList<RecipeDto> _recipes = [];

    public static int Count => _recipes.Count;

    /// <summary>Every recipe, in file order. Filtering and presentation are the
    /// caller's concern.</summary>
    public static IReadOnlyList<RecipeDto> All => _recipes;

    public static void Load(params string?[] candidatePaths) =>
        _recipes = JsonCatalogFile.LoadFirst(candidatePaths, Parse) ?? [];

    private static IReadOnlyList<RecipeDto> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<RecipeFile>(json, JsonCatalogFile.Options)
            ?? throw new JsonException("Recipe file is empty.");

        return [.. file.Recipes.Select(r => new RecipeDto(
            r.Name,
            r.Item,
            r.Amount,
            r.Station,
            r.StationToken,
            r.MinStationLevel,
            r.Craftable,
            r.RequireOnlyOneIngredient,
            r.Season,
            [.. r.Resources.Select(q => new RecipeResourceDto(q.Item, q.Amount, q.AmountPerLevel, q.Upgrader))]))];
    }

    // The file's own shape, kept private so the DTOs carry no serialization concerns.
    private sealed record RecipeFile(List<RecipeEntry> Recipes);

    private sealed record RecipeEntry(
        string Name,
        string Item,
        int Amount,
        string? Station,
        string? StationToken,
        int MinStationLevel,
        bool Craftable,
        bool RequireOnlyOneIngredient,
        string? Season,
        List<RequirementEntry> Resources);

    private sealed record RequirementEntry(string Item, int Amount, int AmountPerLevel, bool Upgrader);
}
