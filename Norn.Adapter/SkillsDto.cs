namespace Norn.Adapter;

/// <summary>One skill entry. <see cref="Type"/> is the skill's name, translated from
/// <c>Norn.GameCore.Skills.SkillType</c> so no GameCore enum crosses the boundary.</summary>
public sealed record SkillDto(string Type, float Level, float Accumulator);

/// <summary>Read-only view for the Skills tab.</summary>
public sealed record SkillsDto(IReadOnlyList<SkillDto> Skills);
