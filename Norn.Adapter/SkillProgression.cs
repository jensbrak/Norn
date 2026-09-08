namespace Norn.Adapter;

/// <summary>
/// Valheim's skill-leveling math, reimplemented here as ordinary feature
/// code — not <c>GameCore</c>, not subject to mirror-discipline, since
/// nothing here affects <c>Read</c>/<c>Write</c> byte-for-byte correctness.
/// Domain knowledge shared by both the write path
/// (<see cref="CharacterEditor.CompensateSkillsForDeath"/>) and the read
/// path (<c>SkillsTabModule</c>'s progress display), which is why it lives
/// in <c>Adapter</c> rather than <c>Norn.UI</c> — unlike
/// <c>Norn.UI/AppearanceColors.cs</c>, which only ever serves one UI
/// control, this is domain-general enough that both layers plausibly want
/// it. Each such fact carries a <c>game-derived:</c> tag naming its source
/// and the game version confirmed against.
/// </summary>
public static class SkillProgression
{
    // game-derived: Skills.Skill.Raise returns false once m_level >= 100f
    // (Valheim 0.221.10/0.221.4, confirmed in both trees) — the
    // game's own level cap.
    public const float MaxLevel = 100f;

    // game-derived: shape from Skills.OnDeath -> LowerAllSkills(factor)
    // (level -= level * factor, multiplicative); magnitude assumes a 5%
    // base loss — the widely-observed community figure, NOT a value the
    // decompiled assembly can confirm. The real factor
    // (Skills.m_DeathLowerFactor) is Unity-prefab-serialized data outside
    // assembly_valheim; its code initializer (0.25) is known not to be the
    // shipped value. Reversing an assumed 5% loss takes 1/0.95 - 1 ≈
    // 5.263%, not a flat +5%. Just a seeded default; freely adjustable.
    public const float DefaultCompensatePercent = 5.263f;

    /// <summary>
    /// Mirrors <c>Skills.Skill.GetNextLevelRequirement()</c>: the
    /// accumulator needed to advance from the current whole level to the
    /// next one. Superlinear — 1.0 at level 0, ~50.5 at level 99 — so
    /// leveling gets harder every level, matching the curve the game
    /// itself uses.
    /// </summary>
    // game-derived: Skills.Skill.GetNextLevelRequirement (Valheim
    // 0.221.10/0.221.4, confirmed in both trees).
    public static float GetNextLevelRequirement(float level) =>
        MathF.Pow(MathF.Floor(level) + 1f, 1.5f) * 0.5f + 0.5f;

    /// <summary>
    /// The inverse of <c>Skills.OnDeath</c>'s own death-loss operation:
    /// <c>level *= 1 + percent/100</c>, clamped to <see cref="MaxLevel"/>.
    /// A skill already at 0 stays 0 — a multiplicative compensation has
    /// nothing to multiply — known, accepted, inherited from Loki.
    /// </summary>
    public static float ApplyDeathCompensation(float level, float percent) =>
        Math.Clamp(level * (1f + percent / 100f), 0f, MaxLevel);
}
