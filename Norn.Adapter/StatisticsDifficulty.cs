namespace Norn.Adapter;

/// <summary>
/// The ten statistics slots a 1.0 profile carries, and what each one means.
/// </summary>
/// <remarks>
/// Valheim 1.0 replaced the profile's single flat statistics block with ten of
/// them. This is the vocabulary for that, kept in one place
/// rather than inline in a mapper or a tab module.
///
/// <para>
/// The name "difficulty" is only three-quarters true and the distinction
/// matters for how these get presented: slots 0-2 are not difficulty tiers at
/// all. <see cref="RawStats"/> counts everything unconditionally, including
/// play the game considers cheated; <see cref="Any"/> counts everything that
/// passed the cheat check; <see cref="Hammer"/> is never written by any of the
/// game's increment paths. Only 3-9 are real combat-difficulty tiers.
/// </para>
///
/// <para>
/// The tiers are also cumulative rather than exclusive for name-keyed stats
/// (enemy kills, pickups, crafts, pickables, food): the game credits every tier
/// from <see cref="Casual"/> up to the active one, so a kill on
/// <see cref="Hard"/> also lands in <see cref="Default"/> and below. Scalar
/// stats do not cascade — they go to slot 0, slot 1, and the active tier only.
/// Presenting a tier's numbers as "earned at exactly this difficulty" would
/// therefore be wrong; they mean "at this difficulty or above".
/// </para>
/// </remarks>
// game-derived: DifficultyRequirement (assembly_valheim), member for member.
// The slot a stat is written to is Achievements.GetCurrentAchievementDifficulty(),
// which name-matches ServerOptionsGUI.GetActiveCombatDifficulty() against these
// members and falls back to Any when nothing matches. The cascade described
// above is PlayerProfile.IncrementStatEnemy/ItemPickup/ItemCraft/Pickable/
// FoodEaten, each looping `for (i = 3; i <= current; i++)`.
// Confirmed against 1.0.7.
public enum StatisticsDifficulty
{
    RawStats = 0,
    Any = 1,
    Hammer = 2,
    Casual = 3,
    VeryEasy = 4,
    Easy = 5,
    Default = 6,
    Hard = 7,
    VeryHard = 8,
    Hardcore = 9
}
