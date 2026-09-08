namespace Norn.Adapter;

// game-derived: ItemDrop.ItemData.IsEquipable() (Valheim 0.221.10/0.221.4,
// confirmed identical in both trees) — the guard clause in
// Humanoid.ToggleEquipped/Player's override, i.e. the same check the game
// itself uses to decide whether a player can toggle an item's equipped
// state through its own inventory UI. Deliberately NOT Humanoid.EquipItem's
// broader 15-type chain (adds AmmoNonEquipable, the engine's own
// auto-equip-on-fire path): this
// classification answers "would the game show an equip control for this,"
// which is what a read-only badge in Norn's own UI should mirror.
public static class ItemEquippability
{
    private static readonly HashSet<ItemType> Equipable =
    [
        ItemType.Tool,
        ItemType.OneHandedWeapon,
        ItemType.TwoHandedWeapon,
        ItemType.TwoHandedWeaponLeft,
        ItemType.Bow,
        ItemType.Shield,
        ItemType.Helmet,
        ItemType.Chest,
        ItemType.Legs,
        ItemType.Shoulder,
        ItemType.Ammo,
        ItemType.Torch,
        ItemType.Utility,
        ItemType.Trinket,
    ];

    public static bool IsEquipable(ItemType type) => Equipable.Contains(type);
}
