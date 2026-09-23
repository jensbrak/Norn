using Norn.GameCore.Primitives;

namespace Norn.GameCore;

public static partial class Minimap
{
    // mirrors: Minimap.PinData (wire-persisted fields only)
    // source:  Valheim 1.0.15
    // note:    Source declares sixteen fields; only six reach the wire —
    //          m_name, m_type, m_pos, m_ownerID, m_author, m_checked, the
    //          six declared below. The other ten (m_icon, m_save,
    //          m_shouldDelete, m_doubleSize, m_animate, m_worldSize, and the
    //          UI element references) are live-runtime-only and have no wire
    //          representation, so they are not carried here, matching the
    //          treatment already applied to Player.Food.
    // note:    DIVERGENCE. Source's AddPin clamps an out-of-range m_type to
    //          Icon3 on load, against a bound sized from PinType's own member
    //          count (see the note on PinType itself). Not mirrored: an
    //          out-of-range value is preserved as-is (C# enums place no range
    //          check on an int-cast value) so a viewer can decide how to
    //          render or report it, rather than silently rewriting it.
    // note:    DIVERGENCE. Source's m_author is a PlatformUserID, not a
    //          string; its decompiled type is unavailable (not present in
    //          any decompiled assembly) so it is
    //          held as the raw wire string, which is what the format
    //          actually carries either way.
    // note:    OMITTED SIDE EFFECT. Source's AddPin also coerces a null
    //          m_name to "" on load (Minimap.cs, same method as the type
    //          clamp above). Not reachable here either:
    //          BinaryReader.ReadString never returns null.
    /// <summary>One map pin, as stored inside a world's map-data blob.
    /// Declaration order matches source, not wire order — same convention as
    /// <see cref="PlayerProfile.WorldPlayerData"/>.</summary>
    public sealed class PinData
    {
        public string m_name = "";

        public PinType m_type;

        public Vector3 m_pos = Vector3.zero;

        public long m_ownerID;

        public string m_author = "";

        public bool m_checked;
    }
}
