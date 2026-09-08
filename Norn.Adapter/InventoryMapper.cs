using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>Maps <see cref="Player.m_inventory"/> to <see cref="InventoryDto"/>.</summary>
public static class InventoryMapper
{
    public static InventoryDto Map(Player player)
    {
        var items = player.m_inventory.m_inventory
            .Select(item => new ItemDto(
                // A save at item version 108+ carries no prefab name at all,
                // only a one-way hash of it, so the name has to be resolved
                // back through the catalog. Falls back to "" when the catalog
                // does not know the hash — a modded or newly-added item — which
                // the UI already handles as an unresolvable entry.
                ItemPrefabHashes.Resolve(item.PrefabName, item.PrefabHash),
                item.m_stack,
                item.m_durability,
                item.m_gridPos.x,
                item.m_gridPos.y,
                item.m_equipped,
                item.m_quality,
                item.m_variant,
                item.m_crafterID,
                item.m_crafterName,
                item.m_customData.ToList(),
                item.m_worldLevel,
                item.m_pickedUp))
            .ToList();

        return new InventoryDto(items);
    }
}
