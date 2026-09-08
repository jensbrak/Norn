using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// Resolves the stable prefab hash stored by item version 108+ back to a
/// prefab name, using the item catalog Norn already ships.
/// </summary>
/// <remarks>
/// <para>
/// Valheim 1.0 stopped storing an item's prefab name and stores a hash of it
/// instead. The hash is one-way, so a save file alone no longer says what any
/// of its items are — which would leave the inventory showing nothing but
/// numbers.
/// </para>
/// <para>
/// The way back is not to invert the hash but to hash forwards: take every
/// prefab name Norn already knows from <see cref="SharedItemDataCatalog"/> and
/// build the reverse index once. A hash that is not in the catalog cannot be
/// named at all — that is a genuine limit, not a bug, and callers get
/// <see langword="null"/> so they can show the raw hash rather than a wrong
/// name.
/// </para>
/// <para>
/// This lives in <c>Norn.Adapter</c> rather than <c>Norn.GameCore</c> on
/// purpose. The mirror has no asset database and must not grow one; resolving
/// an identity against catalog data is exactly the domain-shaping work this
/// layer exists for, and both the read path and the write path want it.
/// </para>
/// </remarks>
// game-derived: the hash function itself is GameCore's
// StringExtensionMethods.GetStableHashCode, mirroring assembly_utils. Nothing
// game-derived is duplicated here — this file only applies it to a name list.
// The names come from Norn.UI/Content/SharedItemData.csv, which is asset-
// derived and already tracked as its own patch-day surface.
// Confirmed against 1.0.7.
public static class ItemPrefabHashes
{
    private static Dictionary<int, string>? _byHash;

    /// <summary>
    /// Builds (or rebuilds) the reverse index from the currently loaded
    /// <see cref="SharedItemDataCatalog"/>. Safe to call repeatedly.
    /// </summary>
    /// <remarks>
    /// Rebuilt rather than cached-forever because the catalog itself is loaded
    /// from a path chosen at startup and is reloaded in tests.
    /// </remarks>
    public static void Rebuild()
    {
        var byHash = new Dictionary<int, string>();
        foreach (var item in SharedItemDataCatalog.All)
        {
            // A hash collision across two real prefab names would make one of
            // them unnameable. Keeping the first and ignoring the rest is
            // deliberate: it is stable across runs (catalog order is the CSV's
            // order), where last-wins would depend on enumeration order.
            byHash.TryAdd(StringExtensionMethods.GetStableHashCode(item.ItemName), item.ItemName);
        }

        _byHash = byHash;
    }

    /// <summary>
    /// The prefab name for <paramref name="hash"/>, or <see langword="null"/>
    /// when the catalog holds no prefab with that hash.
    /// </summary>
    public static string? TryResolve(int hash)
    {
        if (hash == 0)
        {
            return null;
        }

        if (_byHash is null)
        {
            Rebuild();
        }

        return _byHash!.GetValueOrDefault(hash);
    }

    /// <summary>
    /// The name to display for an item, preferring the name the file itself
    /// carried and falling back to resolving its hash.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="string.Empty"/> rather than a placeholder when
    /// neither is available, so callers keep the choice of how to present an
    /// unknown item.
    /// </remarks>
    public static string Resolve(string prefabName, int prefabHash)
    {
        return prefabName != "" ? prefabName : TryResolve(prefabHash) ?? "";
    }

    /// <summary>
    /// The catalog entry for an item, resolved by whichever identity the file
    /// actually carried.
    /// </summary>
    /// <remarks>
    /// Every catalog lookup on a loaded item must go through here rather than
    /// through <c>SharedItemDataCatalog.TryFind(item.PrefabName)</c>. From item
    /// version 108 the name is not in the file at all, so a name-keyed lookup
    /// silently returns null for every item in a Valheim 1.0 save — which makes
    /// each catalog-dependent editor action quietly do nothing instead of
    /// failing.
    /// </remarks>
    public static SharedItemDataDto? TryFindShared(Inventory.ItemData item)
    {
        var name = Resolve(item.PrefabName, item.PrefabHash);
        return name == "" ? null : SharedItemDataCatalog.TryFind(name);
    }
}
