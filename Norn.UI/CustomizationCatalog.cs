using System.Text.RegularExpressions;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Turns <see cref="SharedItemDataCatalog"/>'s raw <c>Customization</c>
/// entries into what General's beard/hair pickers actually offer —
/// reproducing the game's own <c>PlayerCustomizaton</c> picker-list
/// filtering, not a raw catalog dump. Presentation-only: which entries a
/// dropdown shows is a UI concern, not a
/// wire-format or domain-general fact either the read or write path needs,
/// so this lives in <c>Norn.UI</c> rather than <c>Norn.Adapter</c>.
/// </summary>
internal static class CustomizationCatalog
{
    // game-derived: PlayerCustomizaton's own picker-list construction
    // — strips underscore-suffixed
    // variant prefabs (e.g. Hair10_2, an alternate model the game's own UI
    // never surfaces as a separate choice), sorts by display name, forces
    // the "None" entry first. Tool-tier gating is deliberately not
    // reproduced: it's a progression gate, not a data-shape fact, and has
    // no meaning in an editor whose purpose is bypassing progression.
    private static readonly Regex VariantSuffix = new(@"_\d+$", RegexOptions.Compiled);

    public static IReadOnlyList<SharedItemDataDto> Beards() => Filter("Beard");

    public static IReadOnlyList<SharedItemDataDto> Hairs() => Filter("Hair");

    /// <summary>
    /// Trophy prefabs offered as bonus beard/hair options — the head
    /// attach-point that renders a beard/hair style doesn't check what the
    /// prefab was actually modeled for, so a trophy mesh renders and plays
    /// fine there even though the game was never built to offer this
    /// Not a game-supported customization choice:
    /// callers must present this set as visually distinct from
    /// <see cref="Beards"/>/<see cref="Hairs"/>, never merged in
    /// indistinguishably. Same trophy set for both beard and hair — there's
    /// no meaningful distinction between the two attach points for a prefab
    /// never designed for either.
    /// </summary>
    public static IReadOnlyList<SharedItemDataDto> Trophies() =>
        SharedItemDataCatalog.AllOfType(ItemType.Trophy)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<SharedItemDataDto> Filter(string prefix) =>
        SharedItemDataCatalog.AllOfType(ItemType.Customization)
            .Where(item => item.ItemName.StartsWith(prefix, StringComparison.Ordinal)
                        && !VariantSuffix.IsMatch(item.ItemName))
            .OrderBy(item => item.ItemName == prefix + "None" ? 0 : 1)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
