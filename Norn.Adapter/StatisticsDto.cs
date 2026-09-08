namespace Norn.Adapter;

/// <summary>One named telemetry value.</summary>
public sealed record StatDto(string Name, float Value);

/// <summary>
/// One statistics slot — everything the profile records for a single
/// <see cref="StatisticsDifficulty"/>.
/// </summary>
/// <param name="Difficulty">Which slot this is.</param>
/// <param name="PlayerStats">The scalar counters, by stat type.</param>
/// <param name="EnemyStats">
/// Enemy kills, indexed by <see cref="KillModifier"/> — always five entries,
/// any of which may be empty.
/// </param>
public sealed record StatisticsSlotDto(
    StatisticsDifficulty Difficulty,
    IReadOnlyList<StatDto> PlayerStats,
    IReadOnlyList<IReadOnlyList<StatDto>> EnemyStats,
    IReadOnlyList<StatDto> ItemPickupStats,
    IReadOnlyList<StatDto> ItemCraftStats,
    IReadOnlyList<StatDto> PickableStats,
    IReadOnlyList<StatDto> FoodEatenStats,
    IReadOnlyList<StatDto> PiecesPlacedStats)
{
    /// <summary>
    /// True when this slot records nothing at all — no scalar stat above zero
    /// and no named entries anywhere.
    /// </summary>
    /// <remarks>
    /// Used to decide whether a slot is worth offering to the user. A profile
    /// always carries all ten slots, and most are empty for most characters:
    /// only slot 0, slot 1 and the difficulties actually played ever fill up,
    /// and <see cref="StatisticsDifficulty.Hammer"/> never does.
    /// </remarks>
    public bool IsEmpty =>
        !PlayerStats.Any(s => s.Value != 0f)
        && EnemyStats.All(list => list.Count == 0)
        && ItemPickupStats.Count == 0
        && ItemCraftStats.Count == 0
        && PickableStats.Count == 0
        && FoodEatenStats.Count == 0
        && PiecesPlacedStats.Count == 0;
}

/// <summary>Read-only view for the Statistics tab.</summary>
/// <param name="Slots">
/// All ten slots, in <see cref="StatisticsDifficulty"/> order. Always ten,
/// including empty ones — the UI decides what to offer, and a caller that
/// wants "only the interesting ones" can filter on
/// <see cref="StatisticsSlotDto.IsEmpty"/>.
/// </param>
public sealed record StatisticsDto(IReadOnlyList<StatisticsSlotDto> Slots)
{
    /// <summary>
    /// The slot that best answers "what has this character done", regardless
    /// of difficulty: <see cref="StatisticsDifficulty.RawStats"/>.
    /// </summary>
    /// <remarks>
    /// This is the honest equivalent of the single flat block every pre-1.0
    /// save carried, and the right default for a UI: it counts everything
    /// unconditionally, including play the game considers cheated.
    /// </remarks>
    public StatisticsSlotDto Raw => Slots[(int)StatisticsDifficulty.RawStats];
}
