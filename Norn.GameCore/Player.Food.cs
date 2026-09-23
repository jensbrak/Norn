namespace Norn.GameCore;

public partial class Player
{
    // mirrors: Player.Food (fields relevant to the wire format only)
    // source:  Valheim 1.0.15
    // note:    `m_item` (a live ItemDrop.ItemData reference, assigned by
    //          reference from the resolved prefab's ObjectDB entry) and
    //          `m_eitr` (derived at runtime from m_item.m_shared.m_foodEitr,
    //          never read or written) are runtime-only — not mirrored.
    // note:    SAVE ASYMMETRY. Source's Save writes only m_name and m_time
    //          per entry; m_health/m_stamina are populated exclusively by the
    //          legacy `v < 25` read branch and are never written back. A
    //          save of a v14-24 food is therefore a real, permanent
    //          information loss in the game itself, not a Norn artefact —
    //          once re-saved at the current player-data version, the health/
    //          stamina values this mirror reads from an old file cannot
    //          round-trip through Player.Save, matching the game exactly.
    public class Food
    {
        public string m_name = "";

        public float m_time;

        public float m_health;

        public float m_stamina;
    }
}
