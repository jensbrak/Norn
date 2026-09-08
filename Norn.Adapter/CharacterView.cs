namespace Norn.Adapter;

/// <summary>
/// One loaded character, aggregating every per-tab DTO. The single model an
/// <c>ITabModule</c> receives, so a new tab needs no new load-path plumbing.
/// </summary>
public sealed record CharacterView(
    MetaDto Meta,
    SkillsDto Skills,
    VitalsDto Vitals,
    InventoryDto Inventory,
    StatisticsDto Statistics,
    WorldsDto Worlds,
    UnlockablesDto Unlockables);
