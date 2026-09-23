namespace Norn.Adapter;

// game-derived: the player inventory container's grid dimensions. Confirmed
// NOT on the wire — Inventory.Load reads only an item-data version and item
// count; width/height are a code literal in the player's own Inventory
// construction, never serialized — so hardcoding is the only option here,
// not a shortcut. 8 columns × 4 rows = 32 slots, confirmed against Valheim
// 1.0.15. If
// a future patch changes the player's base inventory size, this needs
// updating alongside the patch-day pass.
public static class InventoryLayout
{
    public const int Width = 8;
    public const int Height = 4;
}
