namespace Norn.Adapter;

// game-derived: the player inventory container's grid dimensions. Confirmed
// NOT on the wire — Inventory.Load reads only an item-data version and item
// count; width/height are prefab-driven — so
// hardcoding is the only option here, not a shortcut. Sourced from Valheim
// 0.221.10's default player inventory (8 columns × 4 rows = 32 slots,
// matching Loki's own hardcoded shape). If
// a future patch changes the player's base inventory size, this needs
// updating alongside the patch-day pass.
public static class InventoryLayout
{
    public const int Width = 8;
    public const int Height = 4;
}
