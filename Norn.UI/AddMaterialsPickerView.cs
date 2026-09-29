using Norn.Adapter;

namespace Norn.UI;

/// <summary>One row in the Add Materials window, and the text to show for it.</summary>
public abstract record MaterialsRow(string Label);

/// <summary>A recipe, with the item it produces.</summary>
public sealed record RecipeRow(RecipeDto Recipe, SharedItemDataDto Item, string Label) : MaterialsRow(Label);

/// <summary>A build piece.</summary>
public sealed record PieceRow(PieceDto Piece, string Label) : MaterialsRow(Label);

public enum MaterialsKind
{
    Recipe,
    Piece,
}

/// <summary>
/// What the list shows: everything (<see cref="Kind"/> null), one kind, or one
/// kind narrowed to an output <see cref="ItemType"/> (recipes) or a
/// <see cref="AddMaterialsPickerView.PieceGroup"/> (pieces).
/// </summary>
public readonly record struct MaterialsFilter(MaterialsKind? Kind = null, ItemType? ItemType = null, string? PieceGroup = null);

/// <summary>
/// Computes the Add Materials window's visible list. Pure function, same
/// shape as <see cref="AddItemPickerView"/>: feed it data, assert the result.
/// </summary>
public static class AddMaterialsPickerView
{
    private const string UnresolvedNamePrefix = "$";

    /// <summary>
    /// Recipes whose output resolves via <paramref name="findItem"/>, and
    /// pieces named via <paramref name="findText"/> (falling back to their
    /// token), filtered and searched by the shown name. Left out: recipes
    /// whose output doesn't resolve, and recipes that take just one of their
    /// ingredients (the game picks which at craft time, so there is no single
    /// set of materials to add).
    /// </summary>
    public static IReadOnlyList<MaterialsRow> Apply(
        IReadOnlyList<RecipeDto> recipes,
        IReadOnlyList<PieceDto> pieces,
        Func<string, SharedItemDataDto?> findItem,
        Func<string, string?> findText,
        string searchText,
        MaterialsFilter filter)
    {
        IEnumerable<MaterialsRow> rows = [];

        if (filter.Kind is null or MaterialsKind.Recipe)
        {
            rows = rows.Concat(recipes
                .Where(recipe => !recipe.RequireOnlyOneIngredient)
                .Select(recipe => (Recipe: recipe, Item: findItem(recipe.ItemName)))
                .Where(r => r.Item is not null && (filter.ItemType is null || r.Item.ItemType == filter.ItemType))
                .Select(r => new RecipeRow(r.Recipe, r.Item!, RecipeLabel(r.Recipe, r.Item!))));
        }

        if (filter.Kind is null or MaterialsKind.Piece)
        {
            rows = rows.Concat(pieces
                .Where(piece => filter.PieceGroup is null || PieceGroup(piece) == filter.PieceGroup)
                .Select(piece => new PieceRow(piece, findText(piece.NameToken) ?? piece.NameToken)));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            rows = rows.Where(row => row.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Unresolved "$token" names last, as in AddItemPickerView.
        var ordered = rows
            .OrderBy(row => row.Label.StartsWith(UnresolvedNamePrefix) ? 1 : 0)
            .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Two rows can still share a label (distinct items with one display
        // name, or a recipe and a piece). Told apart by the underlying name,
        // only where the visible list actually needs it — same rule as
        // AddItemPickerView.
        var ambiguous = ordered
            .GroupBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. ordered.Select(row => ambiguous.Contains(row.Label)
            ? row with { Label = $"{row.Label} ({UnderlyingName(row)})" }
            : row)];
    }

    /// <summary>
    /// How a piece is grouped in the filter: by its build-menu category for the
    /// Hammer, whose menu has category tabs, and by tool for the Hoe and the
    /// Cultivator, whose menus don't.
    /// </summary>
    public static string PieceGroup(PieceDto piece) =>
        piece.Tool == "Hammer" ? TabRows.Humanize(piece.Category.ToString()) : piece.Tool;

    // The output's name, plus how many one craft makes when that's more than one.
    private static string RecipeLabel(RecipeDto recipe, SharedItemDataDto item) =>
        recipe.Amount > 1 ? $"{item.DisplayName} ×{recipe.Amount}" : item.DisplayName;

    private static string UnderlyingName(MaterialsRow row) => row switch
    {
        RecipeRow r => r.Recipe.Name,
        PieceRow p => p.Piece.Name,
        _ => "",
    };
}
