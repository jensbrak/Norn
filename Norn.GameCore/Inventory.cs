using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: Inventory
// source:  Valheim 1.0.20
// note:    LOCATION DIVERGENCE. m_inventory (the Inventory instance) is
//          declared on Humanoid in source, not Player; Player inherits it.
//          GameCore's Player holds an Inventory instance directly, since
//          Humanoid/Character are not otherwise mirrored.
public partial class Inventory
{
    // note: VISIBILITY DIVERGENCE. private in source, exposed only via
    // GetAllItems() returning the live list. Made public directly here so
    // Adapter can reach it without an accessor round-trip.
    public List<ItemData> m_inventory = new List<ItemData>();

    // mirrors: Inventory.Load(ZPackage)
    // source:  Valheim 1.0.20
    // note:    OMISSION, accepted. Source hands every item it reads to
    //          AddItem, whose post-read semantics are not mirrored here, on
    //          this path or LoadOld's. AddItem drops the item when ObjectDB
    //          cannot resolve the prefab (by hash here, by the hashed stored
    //          name in LoadOld) or the prefab has no ItemDrop; clamps the
    //          stack to the prefab's max; assigns quality through SetQuality,
    //          which keeps the value; resets the variant to 0 when it is
    //          greater than 0 and greater than or equal to the prefab's
    //          declared variant count (NEW AT 1.0.17, no version gate);
    //          truncates world level through a byte cast; and sets the
    //          cheated flag to the stored flag OR "total damage exceeds
    //          10000f", so an item the file never marked cheated can come out
    //          marked. Since 1.0.20 the variant reset and the damage test both
    //          live in ItemDrop.EnsureValid (the test via
    //          CheatUtils.HasCheatedProperties), called after the cheated
    //          assignment; the stored flag it ORs with is already false for
    //          item version 109 (see ItemData.m_cheated). It then
    //          places the item at its stored grid position: merged into a
    //          same-type stack already there as far as that stack has room
    //          (any remainder is lost), otherwise put in that exact slot.
    //          There is no relocation on either path. An out-of-grid position
    //          is discarded, with one asymmetry: skipValidPositionCheck
    //          suppresses only the "y beyond the inventory height" term, and
    //          this path passes it true while LoadOld passes false, so here
    //          an item with an out-of-range y is placed rather than
    //          discarded; a negative coordinate or an out-of-range x is
    //          discarded on both. Reproducing any of this needs ObjectDB, an
    //          asset database this standalone reader does not have, and is
    //          exactly what makes the game's own load path non-identity.
    //          Every value is kept verbatim instead, including ones the game
    //          would drop or alter.
    // note:    CLEAR ORDER CHANGED AT 1.0.7. Through 0.221.10 source read the
    //          version and count first and cleared the list afterwards; it now
    //          clears first, before reading anything. No byte-level
    //          consequence, but the previous mirror carried an explicit note
    //          asserting the old order, so the reversal is called out rather
    //          than silently followed.
    // note:    OMISSION, accepted. Source calls `this.Changed(false, false)`
    //          at the end (it took no arguments at 0.221.10), which recomputes
    //          total weight and invokes the m_onChanged delegate — runtime/UI
    //          bookkeeping with no bearing on the byte format, consuming no
    //          bytes. Not carried.
    // note:    OMISSION, accepted, NEW AT 1.0.7 and the same shape as
    //          LoadOld's empty-prefab-name skip. On the compact path source drops
    //          any item whose prefab hash read back as 0, calling AddItem only
    //          when it is non-zero. This mirror keeps every item, for the same
    //          reason it keeps items with an empty legacy name: the bytes have
    //          already been consumed, and dropping the item would make the
    //          writer emit a shorter inventory than it read, breaking R1 on a
    //          file the game itself would have quietly pruned. The hash is
    //          still stored on the item (ItemData.PrefabHash), so a caller
    //          that wants the game's behaviour can apply it.
    // note:    TWO SOURCE OVERLOADS, ONE HERE. Source declares
    //          `Load(ZPackage)` and `Load(ZPackage, bool _)` with byte-for-byte
    //          identical bodies; the bool is read by neither. Only the
    //          single-argument form is carried.
    public void Load(ZPackage pkg)
    {
        m_inventory.Clear();
        Version.Item version = (Version.Item)pkg.ReadInt();
        if (version >= Version.Item.Smaller)
        {
            int count = pkg.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                ItemData item = new ItemData();
                ItemData.Load(pkg, item, version);
                m_inventory.Add(item);
            }
        }
        else
        {
            LoadOld(pkg, version);
        }
    }

    // mirrors: Inventory.Save(ZPackage)
    // source:  Valheim 1.0.20
    // note:    COLLAPSED TO THREE LINES AT 1.0.7. The per-item field sequence
    //          that used to live here moved onto ItemData.Save when the
    //          compact record landed; this method now writes only the version
    //          and the count.
    // note:    THE COUNT IS A ushort, NOT AN int. That is the field-width
    //          change, and it caps a saved inventory at 65,535 items —
    //          source truncates rather than guarding, so a larger inventory
    //          wraps silently. Transcribed as written.
    // note:    OldSave(ZPackage) is NOT mirrored. Source keeps the previous
    //          writer alongside this one (it writes 106, an int count, and the
    //          old inline sequence PLUS a trailing cheated bool — a shape that
    //          matches neither this method nor the 0.221.10 mirror it
    //          replaced). No caller for it was found on the .fch path. It is
    //          recorded here rather than carried, because if it ever does turn
    //          out to be reachable, its shape difference matters.
    public void Save(ZPackage pkg)
    {
        pkg.Write(110);
        pkg.Write((ushort)m_inventory.Count);
        foreach (ItemData item in m_inventory)
        {
            item.Save(pkg);
        }
    }

    // mirrors: Inventory.LoadOld(ZPackage, Version.Item)
    // source:  Valheim 1.0.20
    // note:    NEW METHOD AT 1.0.7 in name only — this is the 0.221.10 read
    //          loop, extracted into its own method when the compact path
    //          landed. Every gate below is unchanged from the previous mirror;
    //          only the cheated read at the end is new.
    // note:    THE CHEATED GATE IS WRITTEN `version == AbandonedDN(107) ||
    //          version >= ChunksNCheats(109)` IN SOURCE, and the second half
    //          is unreachable here: Load only calls this method when the
    //          version is below Smaller(108), so `>= 109` can never hold. The
    //          effective condition is `version == 107`. Source's written form
    //          is reproduced anyway — a mirror transcribes what is there,
    //          including a branch the caller makes dead, because the next
    //          patch may change which caller reaches it.
    // note:    SOURCE'S `if (text != "")` SKIP is not mirrored, same as
    //          Load's zero-hash skip above: an item with an empty prefab name is kept
    //          verbatim so it round-trips, rather than dropped as the game
    //          would drop it.
    // note:    PrefabHash IS COMPUTED HERE, from the stored name. Source does
    //          the equivalent by resolving the name through ObjectDB into the
    //          m_dropPrefab GameObject whose name the writer then re-hashes;
    //          with no ObjectDB, hashing the stored name directly reaches the
    //          same value. An empty name leaves the hash at 0, which is
    //          exactly what source's null m_dropPrefab produces.
    private void LoadOld(ZPackage pkg, Version.Item version)
    {
        int count = pkg.ReadInt();
        for (int i = 0; i < count; i++)
        {
            ItemData item = new ItemData();
            item.PrefabName = pkg.ReadString();
            item.m_stack = pkg.ReadInt();
            item.m_durability = pkg.ReadSingle();
            item.m_gridPos = pkg.ReadVector2i();
            item.m_equipped = pkg.ReadBool();

            if (version >= Version.Item.Quality)
            {
                item.m_quality = pkg.ReadInt();
            }

            if (version >= Version.Item.Variant)
            {
                item.m_variant = pkg.ReadInt();
            }

            if (version >= Version.Item.CrafterID)
            {
                item.m_crafterID = pkg.ReadLong();
                item.m_crafterName = pkg.ReadString();
            }

            if (version >= Version.Item.CustomData)
            {
                int customDataCount = pkg.ReadInt();
                for (int j = 0; j < customDataCount; j++)
                {
                    OrderedCollections.Set(item.m_customData, pkg.ReadString(), pkg.ReadString());
                }
            }

            if (version >= Version.Item.WorldLevel)
            {
                item.m_worldLevel = pkg.ReadInt();
            }

            if (version >= Version.Item.PickedUp)
            {
                item.m_pickedUp = pkg.ReadBool();
            }

            if (version == Version.Item.AbandonedDN || version >= Version.Item.ChunksNCheats)
            {
                item.m_cheated = pkg.ReadBool();
            }

            item.PrefabHash = item.PrefabName != ""
                ? StringExtensionMethods.GetStableHashCode(item.PrefabName)
                : 0;

            m_inventory.Add(item);
        }
    }
}
