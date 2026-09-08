namespace Norn.Adapter;

/// <summary>
/// How an enemy was killed. Valheim 1.0 records enemy kills once per category
/// as well as in a running total, so each statistics slot holds five separate
/// kill tallies rather than one.
/// </summary>
/// <remarks>
/// <see cref="MixedAndTotal"/> is the one to show by default: the game
/// increments it for every kill regardless of how it happened, so it is the
/// real "enemies killed" figure. The other four are breakdowns of it and do not
/// sum to it — a kill counts toward the total unconditionally but toward a
/// category only when the game can attribute one.
/// </remarks>
// game-derived: KillModifiers (assembly_valheim), member for member.
// PlayerProfile.IncrementStatEnemy writes index 0 unconditionally and the
// attributed index only when the modifier is not MixedAndTotal. CountNone is a
// guard value the game passes to mean "do not count this at all"; it is out of
// range of the five-element array by construction and never indexes it, which
// is why it has no counterpart here.
// Confirmed against 1.0.7.
public enum KillModifier
{
    MixedAndTotal = 0,
    Unarmed = 1,
    Magic = 2,
    Ranged = 3,
    Melee = 4
}
