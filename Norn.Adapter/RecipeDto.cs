namespace Norn.Adapter;

/// <summary>
/// One crafting recipe as the game registers it (<c>Recipe</c>), from
/// <see cref="RecipeCatalog"/>. Game facts only — <see cref="CraftingCosts"/>
/// turns them into amounts to add.
/// </summary>
/// <param name="Name">The recipe asset's own name (<c>Recipe_Bronze5</c>) — unique,
/// unlike <paramref name="ItemName"/>: several recipes can produce one item.</param>
/// <param name="Amount">How many of the item one craft produces.</param>
/// <param name="Station">The crafting station's prefab name, or <c>null</c> for a
/// recipe crafted by hand.</param>
/// <param name="StationToken">The station's localization token
/// (<c>$piece_forge</c>), or <c>null</c> with <paramref name="Station"/>.</param>
/// <param name="Craftable"><c>false</c> for a recipe that only upgrades an item
/// obtained some other way (the game's <c>m_noCraftOnlyUpgrade</c>).</param>
/// <param name="Season">The season that switches the recipe on (<c>Halloween</c>),
/// or <c>null</c> for one that's always available.</param>
public sealed record RecipeDto(
    string Name,
    string ItemName,
    int Amount,
    string? Station,
    string? StationToken,
    int MinStationLevel,
    bool Craftable,
    bool RequireOnlyOneIngredient,
    string? Season,
    IReadOnlyList<RecipeResourceDto> Resources);

/// <summary>One requirement of a recipe (<c>Piece.Requirement</c>).</summary>
/// <param name="Upgrader">Asked for only at an upgrader station, never at a normal
/// one — see <see cref="CraftingCosts.Materials"/>.</param>
public sealed record RecipeResourceDto(string ItemName, int Amount, int AmountPerLevel, bool Upgrader);

/// <summary>An amount of one item, by prefab name.</summary>
public readonly record struct ItemAmount(string ItemName, int Amount);
