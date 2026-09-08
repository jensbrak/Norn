using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// Maps <see cref="PlayerProfile"/>'s statistics to <see cref="StatisticsDto"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every name-keyed collection resolves its raw <c>$</c>-prefixed keys
/// (<c>$enemy_asksvin</c>, <c>$item_amber</c>, …) to real English names via
/// <see cref="LocalizationCatalog"/> — the same catalog/resolution pattern
/// <see cref="UnlockablesMapper"/> established, confirmed against real save
/// data to use the identical key shape. The scalar stats are unaffected: their
/// keys are <see cref="PlayerStatType"/> enum names, not localization keys, so
/// there is nothing to resolve.
/// </para>
/// <para>
/// All ten difficulty slots are mapped, not just the one the UI happens to
/// show first. They are cheap (a profile that has only ever been played on one
/// difficulty leaves eight of them empty), and mapping lazily would mean
/// re-decoding on every combo change.
/// </para>
/// </remarks>
public static class StatisticsMapper
{
    public static StatisticsDto Map(PlayerProfile profile)
    {
        var slots = new List<StatisticsSlotDto>(profile.m_playerStats.Length);
        for (var slot = 0; slot < profile.m_playerStats.Length; slot++)
        {
            slots.Add(MapSlot((StatisticsDifficulty)slot, profile.m_playerStats[slot]));
        }

        return new StatisticsDto(slots);
    }

    private static StatisticsSlotDto MapSlot(StatisticsDifficulty difficulty, PlayerProfile.PlayerStats stats)
    {
        // PlayerStatType.Count is a sentinel bound, not a real stat — see
        // PlayerStatType.cs — and 1.0.7 added None after it, which is not a
        // stat either. The profile pre-seeds a key for every index up to the
        // constructor's literal bound, so both must be filtered out explicitly
        // rather than relying on the source list to omit them.
        var playerStats = stats.m_stats
            .Where(pair => pair.Key != PlayerStatType.Count && pair.Key != PlayerStatType.None)
            .OrderBy(pair => (int)pair.Key)
            .Select(pair => new StatDto(pair.Key.ToString(), pair.Value))
            .ToList();

        return new StatisticsSlotDto(
            difficulty,
            playerStats,
            stats.m_enemyStats.Select(MapResolvedPairs).ToList(),
            MapResolvedPairs(stats.m_itemPickupStats),
            MapResolvedPairs(stats.m_itemCraftStats),
            MapResolvedPairs(stats.m_pickableStats),
            MapResolvedPairs(stats.m_foodEatenStats),
            MapResolvedPairs(stats.m_piecesPlacedStats));
    }

    private static IReadOnlyList<StatDto> MapResolvedPairs(List<KeyValuePair<string, float>> pairs)
        => pairs.Select(pair => new StatDto(Resolve(pair.Key), pair.Value)).ToList();

    /// <summary>Sanitizes before lookup/fallback, matching
    /// <see cref="UnlockablesMapper.Resolve"/>'s identical pattern against
    /// the same class of raw wire keys — some carry embedded control
    /// characters (<see cref="UnlockablesMapper"/>'s own doc comment)
    /// "not unique to that one field" (found in review: this call was
    /// missing it).</summary>
    private static string Resolve(string key)
    {
        var clean = UnlockablesMapper.Sanitize(key);
        return LocalizationCatalog.TryFind(clean) ?? clean;
    }
}
