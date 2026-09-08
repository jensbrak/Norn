using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: ItemDrop.ItemData (fields on the .fch wire path only)
// source:  Valheim 1.0.7
// note:    LOCATION DIVERGENCE. Declared nested inside ItemDrop (a
//          MonoBehaviour not otherwise mirrored) in source; nested inside
//          Inventory here instead.
// note:    The overwhelming majority of ItemDrop.ItemData's real fields
//          (m_shared — the per-prefab SharedData: localization token, item
//          type, damages, food values, roughly 180 fields in all) are NOT on
//          the wire at all. They are re-resolved from ObjectDB by prefab name
//          at load. Only the twelve fields below ever touch a .fch.
// note:    [NonSerialized] on several of these fields in source governs Unity
//          Inspector serialization only and has no correlation with what
//          Inventory.Save actually writes — it is not a guide to this format.
// note:    Field declaration order below follows wire/read order, not
//          source's actual declaration order (source: m_stack, m_durability,
//          m_quality, m_variant, m_worldLevel, m_pickedUp, m_shared [not
//          mirrored], m_crafterID, m_crafterName, m_customData, m_gridPos,
//          m_equipped, m_dropPrefab [not mirrored, see PrefabName below]) —
//          same choice already made for Player's field block (Player.cs),
//          for the same reason: a reader following the byte layout benefits
//          more from read order than declaration order here. Contrast
//          PlayerProfile, whose field block instead preserves source's
//          declaration order verbatim — the two mirrored types made
//          opposite choices, each documented at its own declaration.
public partial class Inventory
{
    public class ItemData
    {
        // note: NOT IN SOURCE AS A FIELD. Source has no stored prefab-name
        // field at all — the legacy writer wrote m_dropPrefab.name, a live
        // GameObject reference resolved at runtime via ObjectDB, writing ""
        // if that reference is null. A standalone reader has no GameObject,
        // so this is a plain string field with no game counterpart to name
        // it after.
        // note: LEGACY PATH ONLY FROM ITEM VERSION 108. The compact record
        // stores a hash instead of a name and there is no inverse, so this
        // field is populated only when the save was written at 107 or below.
        // On a 108+ save it stays empty and PrefabHash carries the identity.
        public string PrefabName = "";

        // note: NOT IN SOURCE AS A FIELD either, and new at 1.0.7. Source's
        // single m_dropPrefab (a GameObject) is what both paths converge on:
        // the legacy reader resolves it from the stored name via ObjectDB, the
        // compact reader resolves it from the stored hash, and the writer
        // emits m_dropPrefab.name.GetStableHashCode() either way. With no
        // ObjectDB, this mirror keeps the hash itself as the one identity the
        // writer needs.
        // note: 0 MEANS "NO PREFAB", matching source exactly — the compact
        // record's flag bit 0x40 is set iff m_dropPrefab != null, and when it
        // is clear the reader yields 0 and the game drops the item. Legacy
        // reads compute this from the name (see Inventory.LoadOld), which is
        // the same resolution the game performs through ObjectDB; a legacy
        // item whose stored name was "" keeps 0 here, as it would there.
        public int PrefabHash;

        public int m_stack = 1;

        public float m_durability = 100f;

        public Vector2i m_gridPos = Vector2i.zero;

        public bool m_equipped;

        public int m_quality = 1;

        public int m_variant;

        public long m_crafterID;

        public string m_crafterName = "";

        // note: TYPE DIVERGENCE. .NET does not contract Dictionary's
        // enumeration order, so an ordered form is held instead, for a
        // Dictionary<string, string> that source rebuilds fresh per item
        // (insertion is the indexer — overwrite in place).
        public List<KeyValuePair<string, string>> m_customData = new List<KeyValuePair<string, string>>();

        // note: NOT IN SOURCE default. Source's field initializer is the
        // live static Game.m_worldLevel (current world difficulty) —
        // meaningless before any world is loaded. Defaults to 0 here.
        public int m_worldLevel;

        public bool m_pickedUp;

        // note: NEW ON THE WIRE AT 1.0.7. [NonSerialized] bool in source —
        // which, per the header note above, says nothing about whether it is
        // written. It is: the compact record's trailing flag byte carries it
        // in bit 0x01, and item version 107 carried it as a plain trailing
        // bool. Item version 108 alone has no cheated byte at all.
        public bool m_cheated;

        // mirrors: ItemDrop.ItemData.Save(ZPackage)
        // source:  Valheim 1.0.7
        // note:    NEW METHOD AT 1.0.7. Through 0.221.10 the item record was
        //          written inline by Inventory.Save; the per-item write moved
        //          onto ItemData when the compact layout landed at item
        //          version 108. Inventory.Save now writes only the version and
        //          count and delegates here.
        // note:    THE FLAG BYTE IS BUILT BEFORE ANY WRITE, and every
        //          conditional write below tests a bit of it rather than
        //          re-testing the field. Transcribed that way deliberately:
        //          collapsing `if ((flags & 4) != 0) Write(quality)` back into
        //          `if (m_quality != 1)` would be equivalent today and would
        //          silently diverge the moment source changes one of the two.
        // note:    SOURCE WRITES CRAFTER ID AND CRAFTER NAME IN TWO SEPARATE
        //          `if` BLOCKS, both testing bit 0x20. Kept as two blocks.
        // note:    THE CUSTOM-DATA LOOP SITS OUTSIDE ITS GUARD in source — bit
        //          0x80 gates only the count write, not the key/value loop.
        //          Harmless (the loop is empty exactly when the bit is clear)
        //          and transcribed as written, because it is the kind of
        //          asymmetry a future patch might fix, and the diff should
        //          show that when it happens.
        // note:    FIVE TRUNCATING WRITES. Grid position (per axis), world
        //          level, quality, stack and — at the Inventory level — the
        //          item count are all narrowed on write, and durability is
        //          quantised to hundredths through a truncating int cast. None
        //          of these are reversible in general. See the R1 note on
        //          Load below.
        public void Save(ZPackage pkg)
        {
            int num = (int)(m_durability * 100f);
            bool flag = PrefabHash != 0;
            int num2 = 0;
            num2 |= m_pickedUp ? 1 : 0;
            num2 |= m_equipped ? 2 : 0;
            num2 |= (m_quality != 1) ? 4 : 0;
            num2 |= (m_stack != 1) ? 8 : 0;
            num2 |= (m_variant != 0) ? 16 : 0;
            num2 |= (m_crafterID != 0L) ? 32 : 0;
            num2 |= flag ? 64 : 0;
            num2 |= (m_customData.Count != 0) ? 128 : 0;
            pkg.Write(num);
            pkg.Write((byte)m_gridPos.x);
            pkg.Write((byte)m_gridPos.y);
            pkg.Write((byte)m_worldLevel);
            pkg.Write((byte)num2);
            if ((num2 & 4) != 0)
            {
                pkg.Write((ushort)m_quality);
            }

            if ((num2 & 8) != 0)
            {
                pkg.Write((ushort)m_stack);
            }

            if ((num2 & 16) != 0)
            {
                pkg.Write(m_variant);
            }

            if ((num2 & 32) != 0)
            {
                pkg.Write(m_crafterID);
            }

            if ((num2 & 32) != 0)
            {
                pkg.Write(m_crafterName);
            }

            if ((num2 & 64) != 0)
            {
                pkg.Write(PrefabHash);
            }

            if ((num2 & 128) != 0)
            {
                pkg.WriteNumItems(m_customData.Count);
            }

            foreach (KeyValuePair<string, string> pair in m_customData)
            {
                pkg.Write(pair.Key);
                pkg.Write(pair.Value);
            }

            int num3 = 0;
            num3 |= m_cheated ? 1 : 0;
            pkg.Write((byte)num3);
        }

        // mirrors: ItemDrop.ItemData.Load(ZPackage, Version.Item) [static, and
        // the ValueTuple-returning overload that wraps it]
        // source:  Valheim 1.0.7
        // note:    SIGNATURE DIVERGENCE, two overloads collapsed to one.
        //          Source has `static (int, ItemData) Load(ZPackage,
        //          Version.Item)` whose entire body is `new ItemData()` plus a
        //          call to `static int Load(ZPackage, ItemData,
        //          Version.Item)`. The three-argument form exists so the game
        //          can reload into an item it already holds; nothing on the
        //          .fch path does, so only the allocating form is carried and
        //          it returns the hash the same way. This is the one place in
        //          this file where a source method has no counterpart, and it
        //          is a genuine (if small) decomposition divergence.
        // note:    RETURNS THE PREFAB HASH, which the caller uses to decide
        //          whether the item survives. It is also assigned to
        //          PrefabHash here so the writer can re-emit it — source has
        //          no equivalent assignment because it hands the hash to
        //          AddItem, which resolves it to the m_dropPrefab GameObject
        //          the writer reads back.
        // note:    DEFAULTS FOR GATED-OUT FIELDS ARE SOURCE'S, not
        //          default(T): quality and stack default to 1, variant to 0,
        //          crafter ID to 0 and crafter name to "". Getting one of
        //          these wrong is invisible until a round trip changes an
        //          item.
        // note:    R1 RISK, UNRESOLVED AT THE TIME OF WRITING. Durability is
        //          stored as `(int)(m_durability * 100f)` and read back as
        //          `value * 0.01f`. The write is a truncating cast, not a
        //          round, so `Write(Read(f))` is byte-identical only if
        //          `(int)((n * 0.01f) * 100f) == n` for every n a real save
        //          contains. That has NOT been verified exhaustively. If the
        //          corpus shows it failing, this region downgrades to R2 and
        //          FORMAT.md needs the weakened gate recorded, rather than the
        //          test being quietly relaxed.
        public static int Load(ZPackage pkg, ItemData itemData, Version.Item itemVersion)
        {
            itemData.m_customData = new List<KeyValuePair<string, string>>();
            itemData.m_durability = pkg.ReadInt() * 0.01f;
            itemData.m_gridPos.x = pkg.ReadByte();
            itemData.m_gridPos.y = pkg.ReadByte();
            itemData.m_worldLevel = pkg.ReadByte();
            byte b = pkg.ReadByte();
            itemData.m_pickedUp = (b & 1) > 0;
            itemData.m_equipped = (b & 2) > 0;
            itemData.m_quality = ((b & 4) == 0) ? 1 : pkg.ReadUShort();
            itemData.m_stack = ((b & 8) == 0) ? 1 : pkg.ReadUShort();
            itemData.m_variant = ((b & 16) == 0) ? 0 : pkg.ReadInt();
            itemData.m_crafterID = ((b & 32) == 0) ? 0L : pkg.ReadLong();
            itemData.m_crafterName = ((b & 32) == 0) ? "" : pkg.ReadString();
            int num = ((b & 64) == 0) ? 0 : pkg.ReadInt();
            int num2 = ((b & 128) == 0) ? 0 : pkg.ReadNumItems();
            for (int i = 0; i < num2; i++)
            {
                OrderedCollections.Set(itemData.m_customData, pkg.ReadString(), pkg.ReadString());
            }

            if (itemVersion >= Version.Item.ChunksNCheats || itemVersion == Version.Item.AbandonedDN)
            {
                byte b2 = pkg.ReadByte();
                itemData.m_cheated = (b2 & 1) > 0;
            }

            itemData.PrefabHash = num;
            return num;
        }
    }
}
