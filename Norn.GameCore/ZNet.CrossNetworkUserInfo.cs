using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: ZNet
// source:  Valheim 1.0.7
// note:    STUB. ZNet is the game's networking singleton and nothing else about
//          it is on any load/save path this project mirrors. It exists here
//          solely to host CrossNetworkUserInfo, which 1.0.7 made reachable from
//          World.Load via the new world player-history block. Same treatment
//          Minimap.PinData already gets: the nested type keeps its source
//          nesting rather than being promoted to a top-level type, so a
//          side-by-side diff still lines up.
public partial class ZNet
{
    // mirrors: ZNet.CrossNetworkUserInfo
    // source:  Valheim 1.0.7
    // note:    NEW ON THIS PATH AT 1.0.7. Reached only from World.Load's
    //          m_playerHistory block (world version >= 41).
    // note:    NOT AN EXHAUSTIVE MEMBER LIST. Only Read and the four fields it
    //          populates are carried. Source is a struct implementing
    //          IEquatable with Equals/GetHashCode/operator ==/!=/ToString and a
    //          Write method; none of that is reachable from the read path, and
    //          this mirror has no world writer at all (World.cs is read-only —
    //          see its header).
    // note:    TYPE DIVERGENCE. m_id is a PlatformUserID in source, constructed
    //          as `new PlatformUserID(br.ReadString())`. PlatformUserID is not
    //          present in any decompiled assembly in either the 0.221.x or the
    //          1.0.7 tree, so its parse and ToString behaviour cannot be
    //          verified from source. Held here as the plain string that was
    //          actually on the wire — exactly the treatment
    //          Minimap.PinData.m_author already gets for the same reason, and
    //          the same unresolved gap.
    // note:    CLASS DIVERGENCE. `struct` in source; `class` here. A struct
    //          would work equally well, but every other mirrored record type in
    //          this project (PinData, WorldPlayerData, ItemData) is a class, and
    //          nothing on this path depends on value semantics — the list is
    //          built by Add and never mutated in place.
    /// <summary>One entry in a world's player-history list (world version 41+).</summary>
    public class CrossNetworkUserInfo
    {
        public string m_id;

        public string m_displayName;

        public string m_serverAssignedDisplayName;

        public string m_playfabId;

        public static CrossNetworkUserInfo Read(ZPackage br)
        {
            return new CrossNetworkUserInfo
            {
                m_id = br.ReadString(),
                m_displayName = br.ReadString(),
                m_serverAssignedDisplayName = br.ReadString(),
                m_playfabId = br.ReadString()
            };
        }
    }
}
