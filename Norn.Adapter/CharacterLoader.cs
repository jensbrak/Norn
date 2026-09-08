using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// Top-level read path: a save-file path in, a <see cref="CharacterView"/> or
/// <c>null</c> out. The one entry point the UI needs to open a save.
/// </summary>
public static class CharacterLoader
{
    private static readonly SkillsDto EmptySkills = new([]);
    private static readonly InventoryDto EmptyInventory = new([]);
    private static readonly VitalsDto EmptyVitals = new(0, 0, 0, 0, 0, 0, 0, string.Empty, 0, []);
    private static readonly UnlockablesDto EmptyUnlockables = new([], [], [], [], [], [], [], []);

    /// <summary>
    /// Loads and maps <paramref name="path"/>, or returns <c>null</c> when the
    /// profile is outside the mirror's compatible version range.
    /// </summary>
    public static CharacterView? Load(string path)
    {
        // Same structural pre-check CharacterEditor.Open runs — both are
        // file-opening boundaries, and the allocation hazard it closes
        // doesn't care which one a caller came through.
        SaveFileFrameGuard.ThrowIfFrameImplausible(path);

        var profile = new PlayerProfile(path);
        return profile.Load() ? Map(profile) : null;
    }

    /// <summary>
    /// Maps an already-loaded profile. Shared by <see cref="Load"/> and
    /// <see cref="CharacterEditor"/>, which — unlike <see cref="Load"/> —
    /// retains the <see cref="PlayerProfile"/> across an editing session
    /// instead of discarding it after mapping.
    /// </summary>
    internal static CharacterView Map(PlayerProfile profile)
    {
        var player = PlayerLoader.Load(profile);

        return new CharacterView(
            MetaMapper.Map(profile, player),
            player is not null ? SkillsMapper.Map(player) : EmptySkills,
            player is not null ? VitalsMapper.Map(player) : EmptyVitals,
            player is not null ? InventoryMapper.Map(player) : EmptyInventory,
            StatisticsMapper.Map(profile),
            WorldsMapper.Map(profile),
            player is not null ? UnlockablesMapper.Map(player) : EmptyUnlockables);
    }
}
