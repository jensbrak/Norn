namespace Norn.GameCore;

public static partial class Minimap
{
    // mirrors: Minimap.PinType
    // source:  Valheim 1.0.15
    // note:    Memorial is NEW at 1.0.7, appended after Hildir3. Purely
    //          additive — every existing ordinal is unchanged, so no stored
    //          pin changes meaning.
    // note:    The game clamps an out-of-range stored pin type on read
    //          against m_visibleIconTypes.Length, which Minimap.Start() sizes
    //          directly from this enum's own member count
    //          (`new bool[Enum.GetValues(typeof(Minimap.PinType)).Length]`) —
    //          not asset data, and fully answerable from source. Adding
    //          Memorial does raise that bound, from 17 members at 0.221.10 to
    //          18 at 1.0.7. It does not matter for correctness here — this
    //          mirror deliberately does not apply that clamp, so the raw
    //          stored value survives either way — but it would matter to
    //          anyone who later chose to.
    // note:    No explicit values in source; ordinal positions apply exactly
    //          as declared.
    /// <summary>A map pin's icon/kind, as stored in a pin record.</summary>
    public enum PinType
    {
        Icon0,
        Icon1,
        Icon2,
        Icon3,
        Death,
        Bed,
        Icon4,
        Shout,
        None,
        Boss,
        Player,
        RandomEvent,
        Ping,
        EventArea,
        Hildir1,
        Hildir2,
        Hildir3,
        Memorial
    }
}
