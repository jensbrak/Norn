using Norn.GameCore;
using Norn.GameCore.Primitives;

namespace Norn.Adapter;

/// <summary>Maps <see cref="PlayerProfile"/>/<see cref="Player"/> fields to <see cref="MetaDto"/>.</summary>
public static class MetaMapper
{
    /// <summary>
    /// <paramref name="player"/> is <c>null</c> only when the profile carries no
    /// inner player-data blob at all (see <see cref="PlayerLoader.Load"/>) — a
    /// character that was never actually spawned, not an old save missing some
    /// fields. That second case is already handled entirely inside
    /// <see cref="Player.Load"/> itself, which mirrors the game's own
    /// version-gated reads *and* its own field initializers (e.g.
    /// <c>m_skinColor</c>/<c>m_hairColor</c> default to <c>Vector3.one</c>,
    /// matching source); this
    /// mapper only ever sees the already-resolved value, so it never needs to
    /// know whether a given field came off the wire or from a version-gate
    /// default. The player-sourced fields below fall back to plain type
    /// defaults (0/""/black) only for the true-null case above, same pattern as
    /// the empty DTOs <see cref="CharacterLoader"/> substitutes for
    /// Skills/Vitals/Inventory — these are placeholders for "nothing to show,"
    /// not an attempt to guess the game's real default (which is Unity-prefab
    /// driven and unreachable from decompiled source for this case, same class
    /// of gap already recorded for pre-v7 health).
    /// </summary>
    public static MetaDto Map(PlayerProfile profile, Player? player)
    {
        return new MetaDto(
            profile.m_playerName,
            profile.m_playerID,
            profile.m_startSeed,
            profile.ProfileVersion,
            profile.m_dateCreated,
            profile.m_usedCheats,
            profile.m_firstSpawn,
            player?.PlayerDataVersion ?? 0,
            player?.m_beardItem ?? "",
            player?.m_hairItem ?? "",
            player is not null ? ToColor(player.m_skinColor) : default,
            player is not null ? ToColor(player.m_hairColor) : default,
            player?.m_modelIndex ?? 0,
            player?.m_customData ?? new List<KeyValuePair<string, string>>());
    }

    private static ColorDto ToColor(Vector3 v) => new(v.x, v.y, v.z);
}
