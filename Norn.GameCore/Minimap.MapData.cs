namespace Norn.GameCore;

public static partial class Minimap
{
    // note: NOT IN SOURCE — decode result. Source has no equivalent value
    // type; SetMapData mutates a live Minimap component's fields in place.
    // See Minimap.cs for why Decode returns this instead.
    /// <summary>A world's map-data blob, decoded for display only — see
    /// <see cref="Decode"/>.</summary>
    public sealed class MapData
    {
        /// <summary>The blob's own stored format version. Not compared
        /// against <c>Minimap.MAPVERSION</c> on decode — see Minimap.cs.</summary>
        public int Version;

        /// <summary>The blob's own stored square edge length. A standalone
        /// decoder trusts this to size <see cref="Explored"/> and
        /// <see cref="ExploredOthers"/> — see Minimap.cs.</summary>
        public int TextureSize;

        /// <summary>Row-major, <c>TextureSize * TextureSize</c> entries.
        /// Non-zero means explored by this character.</summary>
        public byte[] Explored = System.Array.Empty<byte>();

        /// <summary>Row-major, same shape as <see cref="Explored"/>.
        /// Non-zero means explored via a shared source (cartography table).</summary>
        public byte[] ExploredOthers = System.Array.Empty<byte>();

        public List<PinData> Pins = new List<PinData>();

        // note: INVENTED DEFAULT below version 4. Source only ever assigns
        // this flag (via ZNet.instance.SetPublicReferencePosition) inside
        // the version >= 4 gate; below that version the live flag simply
        // keeps whatever value it already had, so source has no default to
        // copy for a blob that never sets it. `false` is substituted here
        // and is indistinguishable from a genuinely stored `false` — the
        // best available choice for a value-returning decoder with no prior
        // state, but not a fact recoverable from the blob itself below v4.
        public bool PublicReferencePosition;
    }
}
