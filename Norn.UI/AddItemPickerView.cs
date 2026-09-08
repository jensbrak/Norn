using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// One row in the Add Item window: the catalog entry, and the text to show for it.
/// </summary>
/// <param name="Label">
/// Usually just the display name. Qualified with the prefab name when the visible list
/// holds more than one entry under that same display name — see
/// <see cref="AddItemPickerView.Apply"/>.
/// </param>
public sealed record AddItemRow(SharedItemDataDto Item, string Label);

/// <summary>
/// Computes the Add Item window's visible catalog list from the full set.
/// Pure function — no Avalonia types, no I/O — same testability shape as
/// <see cref="SaveFileListView.Apply"/>: feed it data, assert the result.
/// </summary>
public static class AddItemPickerView
{
    /// <summary>
    /// Marks a display name that is really an unresolved localization token — see the
    /// ordering note in <see cref="Apply"/>.
    /// </summary>
    private const string UnresolvedNamePrefix = "$";

    public static IReadOnlyList<AddItemRow> Apply(
        IReadOnlyList<SharedItemDataDto> all,
        string searchText,
        ItemType? category)
    {
        IEnumerable<SharedItemDataDto> visible = all;

        if (category is not null)
        {
            visible = visible.Where(item => item.ItemType == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            visible = visible.Where(item => item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Items the game itself ships without a translation keep their raw token as a
        // display name ("$item_trophy_deer_white"). That is the right fallback - the
        // alternative is dropping them from the catalog entirely - but '$' sorts ahead of
        // every letter, so a plain alphabetical order puts the eleven least useful entries
        // at the very top of the list. Sorted last instead: still present, still
        // searchable, no longer the first thing anyone sees.
        var ordered = visible
            .OrderBy(item => item.DisplayName.StartsWith(UnresolvedNamePrefix) ? 1 : 0)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Several distinct items legitimately share one display name, and Valheim 1.0
        // made that common: troll leather armour now exists three times over as the
        // craftable item, a FallenWarrior variant found at memorial sites, and a third
        // registered-but-unreachable variant. They are genuinely different items with
        // different prefabs, so the catalog is right to carry all of them.
        //
        // They are NOT collapsed to one row. Every one of them is addable, each writes a
        // different prefab hash into the save, and which one a user wants is not knowable
        // here — the groups do not share a shape (elsewhere it is a feast and its material
        // form, or a craftable crossbow beside a creature's version of the same weapon),
        // so any rule for picking a survivor would be wrong for some of them. Hiding an
        // item a user could otherwise add is a worse failure than showing two rows.
        //
        // Instead they are told apart. The prefab name is the thing that actually differs,
        // so it is what gets shown, and only where it is needed: a name that is unique in
        // the visible list stays clean. Ambiguity is judged against the *visible* list
        // rather than the whole catalog, because that is the list the user is choosing
        // from — filtering to a category that contains only one of a pair makes the
        // qualifier unnecessary, and it disappears.
        var ambiguous = ordered
            .GroupBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ordered
            .Select(item => new AddItemRow(
                item,
                ambiguous.Contains(item.DisplayName)
                    ? $"{item.DisplayName} ({item.ItemName})"
                    : item.DisplayName))
            .ToList();
    }
}
