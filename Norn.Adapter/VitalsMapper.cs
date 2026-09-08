using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>Maps <see cref="Player"/> vital-stat fields to <see cref="VitalsDto"/>.</summary>
public static class VitalsMapper
{
    public static VitalsDto Map(Player player)
    {
        var foods = player.m_foods
            .Select(food => new FoodDto(food.m_name, food.m_time, food.m_health, food.m_stamina))
            .ToList();

        return new VitalsDto(
            player.m_health,
            player.m_maxHealth,
            player.m_stamina,
            player.m_maxStamina,
            player.m_eitr,
            player.m_maxEitr,
            player.m_timeSinceDeath,
            player.m_guardianPower,
            player.m_guardianPowerCooldown,
            foods);
    }
}
