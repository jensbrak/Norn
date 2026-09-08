using Norn.GameCore;
using Norn.GameCore.Primitives;

namespace Norn.Adapter;

/// <summary>Maps <see cref="PlayerProfile"/>'s per-world and known-world data to <see cref="WorldsDto"/>.
/// Also resolves each world's <see cref="WorldIdentityDto"/> against
/// <see cref="WorldIdentityCatalog"/> — same pattern as
/// <see cref="UnlockablesMapper"/> resolving raw wire keys against
/// <see cref="LocalizationCatalog"/>/<see cref="SharedItemDataCatalog"/>,
/// just a locally-scanned catalog instead of a bundled one.</summary>
public static class WorldsMapper
{

    // Slot 0 deliberately, and permanently — not a placeholder the way the
    // Statistics tab's slot-0 read was before it grew a selector. Valheim 1.0
    // records known worlds, world keys and commands once per difficulty slot,
    // but a world's playtime is not a per-difficulty fact anyone would want
    // sliced: "how long have I played here" means all of it. RawStats is the
    // only slot that answers that, since it is the one the game increments
    // unconditionally.
    private const int RawStatsSlot = (int)StatisticsDifficulty.RawStats;

    public static WorldsDto Map(PlayerProfile profile)
    {
        var worlds = profile.m_worldData
            .Select(pair => new WorldDto(
                pair.Key,
                pair.Value.m_haveCustomSpawnPoint,
                ToPosition(pair.Value.m_spawnPoint),
                pair.Value.m_haveLogoutPoint,
                ToPosition(pair.Value.m_logoutPoint),
                pair.Value.m_haveDeathPoint,
                ToPosition(pair.Value.m_deathPoint),
                ToPosition(pair.Value.m_homePoint),
                pair.Value.m_mapData is not null,
                WorldIdentityCatalog.TryFind(pair.Key)))
            .ToList();

        return new WorldsDto(
            worlds,
            profile.m_playerStats[RawStatsSlot].m_knownWorlds.ToList(),
            profile.m_playerStats[RawStatsSlot].m_knownWorldKeys.ToList(),
            profile.m_playerStats[RawStatsSlot].m_knownCommands.ToList());
    }

    private static PositionDto ToPosition(Vector3 v) => new(v.x, v.y, v.z);
}
