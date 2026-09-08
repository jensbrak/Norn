namespace Norn.Adapter;

// game-derived: ItemDrop.ItemData.ItemType (Valheim 0.221.10/0.221.4,
// confirmed identical in both trees). Not wire format — the save
// carries this as a resolved value from the prefab, not a raw byte — so
// it lives here, not in GameCore, and isn't
// subject to mirror-discipline. 24 members, gap at value 8 (no member was
// ever declared there — confirmed via contiguous decompiler field tokens,
// not merely "not found"), Ammo = 9 the only explicit assignment, so the
// highest value is 24 (Trinket), not 23.
public enum ItemType
{
    None = 0,
    Material = 1,
    Consumable = 2,
    OneHandedWeapon = 3,
    Bow = 4,
    Shield = 5,
    Helmet = 6,
    Chest = 7,
    Ammo = 9,
    Customization = 10,
    Legs = 11,
    Hands = 12,
    Trophy = 13,
    TwoHandedWeapon = 14,
    Torch = 15,
    Misc = 16,
    Shoulder = 17,
    Utility = 18,
    Tool = 19,
    Attach_Atgeir = 20,
    Fish = 21,
    TwoHandedWeaponLeft = 22,
    AmmoNonEquipable = 23,
    Trinket = 24,
}
