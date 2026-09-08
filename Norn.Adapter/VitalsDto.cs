namespace Norn.Adapter;

/// <summary>One active-food entry. <see cref="Health"/>/<see cref="Stamina"/> are only
/// ever populated for player-data versions below 25 — see the SAVE ASYMMETRY note on
/// <c>Norn.GameCore.Player.Food</c>; a real save at the current version leaves both at 0.</summary>
public sealed record FoodDto(string Name, float Time, float Health, float Stamina);

/// <summary>Read-only view for the Vitals tab.</summary>
public sealed record VitalsDto(
    float Health,
    float MaxHealth,
    float Stamina,
    float MaxStamina,
    float Eitr,
    float MaxEitr,
    float TimeSinceDeath,
    string GuardianPower,
    float GuardianPowerCooldown,
    IReadOnlyList<FoodDto> ActiveFoods);
