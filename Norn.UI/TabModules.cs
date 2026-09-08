namespace Norn.UI;

/// <summary>
/// The one registration list. Adding a tab means adding a line here — nothing
/// else in the shell changes.
/// </summary>
public static class TabModules
{
    public static readonly IReadOnlyList<ITabModule> All =
    [
        new GeneralTabModule(),
        new VitalsTabModule(),
        new InventoryTabModule(),
        new SkillsTabModule(),
        new WorldsTabModule(),
        new UnlockablesTabModule(),
        new StatisticsTabModule(),
    ];
}
