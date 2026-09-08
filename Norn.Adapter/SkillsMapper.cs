using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>Maps <see cref="Player.m_skills"/> to <see cref="SkillsDto"/>.</summary>
public static class SkillsMapper
{
    public static SkillsDto Map(Player player)
    {
        var skills = player.m_skills.m_skillData
            .Select(skill => new SkillDto(skill.m_type.ToString(), skill.m_level, skill.m_accumulator))
            .ToList();

        return new SkillsDto(skills);
    }
}
