using Norn.GameCore.Primitives;

namespace Norn.GameCore;

public partial class PlayerProfile
{
    // mirrors: PlayerProfile.WorldPlayerData
    // source:  Valheim 1.0.7
    // note:    VISIBILITY DIVERGENCE. Private in source; every public accessor
    //          (GetLogoutPoint, HaveDeathPoint, ...) routes through
    //          ZNet.instance.GetWorldUID(), which a standalone reader has no
    //          equivalent for. Widened to public so Adapter can reach
    //          world data directly. Field shape and read order are unchanged.
    // note:    Declaration order below matches source exactly, and deliberately
    //          does not match serialization order (each vector is declared
    //          before its flag, but written flag-first) — see
    //          PlayerProfile.LoadPlayerFromDisk for the write/read order.
    // note:    The world UID (Int64 dictionary key) is not a field on this class
    //          in source either; it lives only as the m_worldData key.
    /// <summary>Per-world data: spawn/logout/death/home points and map data.</summary>
    public class WorldPlayerData
    {
        public Vector3 m_spawnPoint = Vector3.zero;

        public bool m_haveCustomSpawnPoint;

        public Vector3 m_logoutPoint = Vector3.zero;

        public bool m_haveLogoutPoint;

        public Vector3 m_deathPoint = Vector3.zero;

        public bool m_haveDeathPoint;

        public Vector3 m_homePoint = Vector3.zero;

        /// <summary>
        /// Held opaque: gzip-compressed at map version ≥ 7, and re-emitted
        /// verbatim rather than recompressed, because gzip output is not
        /// byte-reproducible across runtimes. One deliberate exception exists
        /// — the explicit "explore all" edit re-encodes a single world's blob
        /// and claims no byte-identity for it; see <c>Minimap.Encode</c>.
        /// </summary>
        public byte[] m_mapData;
    }
}
