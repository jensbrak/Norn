using System.Globalization;
using CsvHelper;

namespace Norn.Adapter;

/// <summary>
/// Norn's sole seam onto the shared item-data CSV — every other
/// file in Norn depends only on <see cref="TryFind"/>, never on the CSV shape
/// itself, so a future format/content change touches this file alone. Static
/// and load-once, matching Loki's own <c>ItemDb</c> precedent — Norn has
/// no DI container, and a
/// long-lived catalog with an explicit reload point (a future Settings
/// "Import" feature) fits a static better than threading an
/// instance through every mutator. Parses via CsvHelper
/// — originally a hand-rolled RFC-4180-lite line splitter, migrated once
/// <see cref="LocalizationCatalog"/> introduced the dependency anyway for a
/// case (multiline quoted values) the hand-rolled reader genuinely couldn't
/// handle; reusing it here removes a second, differently-scoped CSV-quoting
/// implementation from the codebase rather than leaving the original for
/// this simpler, already-verified shape.
/// </summary>
public static class SharedItemDataCatalog
{
    private static IReadOnlyDictionary<string, SharedItemDataDto> _items =
        new Dictionary<string, SharedItemDataDto>();

    /// <summary>Number of resolved items — the raw material for the future
    /// Import feature's "replaces X items with Y items" prompt.</summary>
    public static int Count => _items.Count;

    /// <summary>Every catalog entry, unfiltered — the raw material for the
    /// Add Item picker (<c>Norn.UI/AddItemPickerView.cs</c>). Same
    /// no-presentation-opinion seam as <see cref="TryFind"/>/<see cref="AllOfType"/>:
    /// order and filtering are entirely the caller's concern.</summary>
    public static IEnumerable<SharedItemDataDto> All => _items.Values;

    /// <summary>
    /// Loads from the first existing, parseable path in
    /// <paramref name="candidatePaths"/>, in order. This method has no opinion
    /// on what "external override" or "bundled default" mean — the caller
    /// encodes that precedence purely by the order it passes paths in
    /// (<c>Norn.UI</c>'s <c>ItemCatalogShim</c> is the only
    /// caller). Leaves the catalog empty — not an error — if no candidate
    /// exists or every candidate fails to parse: a missing or corrupt catalog
    /// degrades every catalog-dependent feature gracefully rather than
    /// blocking the app, matching Loki's own field-verified behavior.
    /// </summary>
    public static void Load(params string?[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (path is null || !File.Exists(path))
            {
                continue;
            }

            try
            {
                _items = ParseFile(path);

                // The prefab-hash reverse index is derived from this catalog,
                // so it has to be rebuilt whenever the catalog changes or it
                // keeps answering from the previous load.
                ItemPrefabHashes.Rebuild();
                return;
            }
            catch
            {
                // Corrupt or unreadable candidate: try the next one, or fall
                // through to empty below. A malformed CSV is exactly the kind
                // of external-data-source failure this seam exists to
                // contain — it must not crash the app.
            }
        }

        _items = new Dictionary<string, SharedItemDataDto>();
        ItemPrefabHashes.Rebuild();
    }

    /// <summary>Resolves a prefab name to its catalog entry, or <c>null</c> on
    /// a miss — a handled case (unresolved item), not an error.</summary>
    public static SharedItemDataDto? TryFind(string itemName)
        => _items.TryGetValue(itemName, out var data) ? data : null;

    /// <summary>
    /// Every catalog entry of one <see cref="ItemType"/>, in whatever order
    /// the backing dictionary holds them — not a display order. Callers that
    /// need one (e.g. Norn.UI's beard/hair pickers) sort themselves; this
    /// method has no opinion on presentation, same seam-scoping reasoning as
    /// <see cref="TryFind"/>. Empty, not an error, when the catalog itself is
    /// empty or has no entries of the requested type.
    /// </summary>
    public static IEnumerable<SharedItemDataDto> AllOfType(ItemType itemType)
        => _items.Values.Where(item => item.ItemType == itemType);

    private static IReadOnlyDictionary<string, SharedItemDataDto> ParseFile(string path)
    {
        var result = new Dictionary<string, SharedItemDataDto>();

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // The header (ItemName,IsTeleportable,...) is read but not validated
        // against the schema: the schema is a known, fixed contract, not
        // something to defend against here.
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            // Fewer columns than the fixed 9-required-field schema: a
            // malformed row, silently skipped — distinct from a field that
            // parses to the wrong *type* below, which still aborts the whole
            // file via Load's own try/catch, matching this method's
            // pre-CsvHelper behavior exactly.
            if (csv.Parser.Count < 9)
            {
                continue;
            }

            var item = ParseRow(csv);
            result[item.ItemName] = item; // last occurrence wins on a duplicate name
        }

        return result;
    }

    private static SharedItemDataDto ParseRow(CsvReader csv)
    {
        // Optional trailing columns — an external-override CSV predating
        // them still parses;
        // TryGetField returns false (leaving the out param at its default,
        // 0) when the column is absent from the header entirely, rather
        // than throwing.
        csv.TryGetField<double>("Weight", out var weight);
        csv.TryGetField<double>("ScaleWeightByQuality", out var scaleWeightByQuality);

        // Default true, not the bool default of false, when the column is
        // absent — see SharedItemDataDto's own doc comment for why "unknown"
        // must not silently become "unsupported" for this one specifically.
        var canHaveCrafterTag = csv.TryGetField<bool>("CanHaveCrafterTag", out var crafterTagValue) ? crafterTagValue : true;

        return new SharedItemDataDto(
            ItemName: csv.GetField("ItemName")!,
            IsTeleportable: bool.Parse(csv.GetField("IsTeleportable")!),
            UsesDurability: bool.Parse(csv.GetField("UsesDurability")!),
            MaxDurability: double.Parse(csv.GetField("MaxDurability")!, CultureInfo.InvariantCulture),
            DurabilityPerLevel: double.Parse(csv.GetField("DurabilityPerLevel")!, CultureInfo.InvariantCulture),
            MaxStack: int.Parse(csv.GetField("MaxStack")!, CultureInfo.InvariantCulture),
            DisplayName: csv.GetField("DisplayName")!,
            MaxQuality: int.Parse(csv.GetField("MaxQuality")!, CultureInfo.InvariantCulture),
            ItemType: ParseItemType(csv.GetField("ItemType")!),
            Weight: weight,
            ScaleWeightByQuality: scaleWeightByQuality,
            CanHaveCrafterTag: canHaveCrafterTag);
    }

    /// <summary>
    /// Validates the raw catalog integer against <see cref="ItemType"/>'s
    /// declared members before casting, falling back to
    /// <see cref="ItemType.None"/> on a miss rather than producing an
    /// undefined enum value. <c>ItemType.cs</c>'s own doc comment records a
    /// gap at value 8 that a future Valheim patch could fill — exactly the
    /// kind of number this catalog could see before Norn's own enum is
    /// updated to match, and an undefined value silently misclassifies under
    /// type-based filtering (e.g. <c>ItemEquippability.Equipable.Contains</c>)
    /// with no diagnostic (found in review).
    /// </summary>
    private static ItemType ParseItemType(string raw)
    {
        var value = int.Parse(raw, CultureInfo.InvariantCulture);
        return Enum.IsDefined(typeof(ItemType), value) ? (ItemType)value : ItemType.None;
    }
}
