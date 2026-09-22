namespace Norn.Adapter;

/// <summary>
/// One catalog entry from the shared item-data CSV — the game's
/// own <c>ItemDrop.ItemData.SharedData</c>, as far as Norn resolves it. Column
/// order matches the CSV header exactly: <c>ItemName,IsTeleportable,
/// UsesDurability,MaxDurability,DurabilityPerLevel,MaxStack,DisplayName,
/// MaxQuality,ItemType,Weight,ScaleWeightByQuality</c>. The last-but-one
/// column parses into the real <see cref="Norn.Adapter.ItemType"/> enum
/// — the property and the enum share a
/// name, which is fine here (a common, unambiguous C# pattern, same shape as
/// e.g. a <c>DateTime DateTime</c> property) and reads more naturally than an
/// artificially distinct name. <see cref="Weight"/>/<see cref="ScaleWeightByQuality"/>
/// are optional trailing columns — a pre-existing external-override CSV
/// (in its per-user location)
/// predates them, so <see cref="SharedItemDataCatalog"/> defaults both to 0
/// when absent rather than rejecting the row. <see cref="CanHaveCrafterTag"/>
/// is the same shape, one column further out —
/// true iff the item is ever the m_item output of an enabled, authoritatively-
/// registered crafting-bench recipe, resolved from the game's own recipe
/// data rather than guessed at from item type.
/// Defaults to true, not false, when the column is absent — the opposite
/// direction from Weight's default, deliberately: an older override CSV
/// predating this column represents "unknown," and the safe reading of
/// "unknown" here is "don't newly restrict a capability that was
/// unconditional before this column existed," not "assume unsupported."
/// </summary>
public sealed record SharedItemDataDto(
    string ItemName,
    bool IsTeleportable,
    bool UsesDurability,
    double MaxDurability,
    double DurabilityPerLevel,
    int MaxStack,
    string DisplayName,
    int MaxQuality,
    ItemType ItemType,
    double Weight,
    double ScaleWeightByQuality,
    bool CanHaveCrafterTag)
{
    /// <summary>
    /// The quality-aware max-durability formula
    /// — mirrors
    /// Loki's own <c>Item.MaxDurability</c>. Shared between
    /// <see cref="CharacterEditor"/>'s repair mutators and the Inventory tab's
    /// display/enablement checks so the formula lives in exactly one place.
    /// </summary>
    public double MaxDurabilityFor(int quality) => MaxDurability + Math.Max(0, quality - 1) * DurabilityPerLevel;

    /// <summary>
    /// The quality-aware per-unit weight formula
    /// (<c>ItemDrop.ItemData.GetNonStackedWeight()</c>) — proportional
    /// per-quality-level scaling, not additive like <see cref="MaxDurabilityFor"/>.
    /// The formula naturally reduces to a plain <see cref="Weight"/> when
    /// <paramref name="quality"/> is 1 or <see cref="ScaleWeightByQuality"/> is
    /// 0, with no explicit branch needed, matching the game's own two guards.
    /// </summary>
    public double WeightFor(int quality) => Weight * (1 + (quality - 1) * ScaleWeightByQuality);

    /// <summary>How far a stored quality sits above this item's catalog
    /// max — 0 at or under max. The only save-observable signal Valheim's
    /// Forge of Potential refinement mechanic leaves behind.</summary>
    public int QualityOverMax(int quality) => Math.Max(0, quality - MaxQuality);
}
