namespace Norn.Adapter;

/// <summary>
/// A summed weight, plus how many of the summed entries the catalog couldn't
/// resolve — those count as 0, so a caller can say the figure is a floor
/// rather than present it as exact.
/// </summary>
public readonly record struct CarryLoad(double Weight, int UnknownItems)
{
    public static CarryLoad operator +(CarryLoad left, CarryLoad right) =>
        new(left.Weight + right.Weight, left.UnknownItems + right.UnknownItems);
}

/// <summary>
/// Valheim's carry weight: what an inventory weighs, how much the character
/// can carry, and how the game rounds both for display. Feature code, not
/// <c>GameCore</c> — none of it is in the save file. The per-item formula
/// itself is <see cref="SharedItemDataDto.WeightFor"/>.
/// </summary>
public static class CarryWeight
{
    // game-derived: Player.m_maxCarryWeight — code default and the live
    // Assets/PrefabInstance/Player.prefab value agree. Confirmed against
    // Valheim 1.0.16.
    public const double BaseCapacity = 300;

    // game-derived: SE_Stats.m_addMaxCarryWeight on the BeltStrength status
    // effect, which the BeltStrength item grants through m_equipStatusEffect
    // for as long as it is equipped. Confirmed against Valheim 1.0.16.
    public const double MegingjordBonus = 150;

    private const string MegingjordPrefabName = "BeltStrength";

    // game-derived: Inventory.UpdateTotalWeight sums ItemData.GetWeight() over
    // every item with no exclusions — equipped items count. Confirmed against
    // Valheim 1.0.16.
    /// <summary>The weight of <paramref name="items"/> as they sit in an inventory.</summary>
    public static CarryLoad LoadOf(IEnumerable<ItemDto> items, Func<string, SharedItemDataDto?> findItem) =>
        Sum(items.Select(item => (findItem(item.PrefabName), item.Quality, item.Stack)));

    /// <summary>The weight <paramref name="items"/> would add as freshly added
    /// items, which are always quality 1.</summary>
    public static CarryLoad LoadOf(IEnumerable<ItemAmount> items, Func<string, SharedItemDataDto?> findItem) =>
        Sum(items.Select(item => (findItem(item.ItemName), 1, item.Amount)));

    // game-derived: Player.GetMaxCarryWeight — m_maxCarryWeight plus every
    // active SE_Stats.m_addMaxCarryWeight. Confirmed against Valheim 1.0.16.
    // Megingjord is the only one of the four non-zero effects a save can
    // show; the rest (CorpseRun, GP_Moder, Potion_strength) are timed buffs
    // that are never saved, so a loaded character starts without them. The
    // game's final multiplier (Game.m_carryWeightRate, a console-only world
    // key) lives in the world, not the character, and is left at its default.
    /// <summary>What the character can carry on entering the game.</summary>
    public static double CapacityOf(IEnumerable<ItemDto> items) =>
        BaseCapacity + (HasMegingjordEquipped(items) ? MegingjordBonus : 0);

    public static bool HasMegingjordEquipped(IEnumerable<ItemDto> items) =>
        items.Any(item => item.Equipped && item.PrefabName == MegingjordPrefabName);

    // game-derived: InventoryGui.UpdateInventoryWeight shows both the total
    // and the capacity through Mathf.CeilToInt. Confirmed against Valheim
    // 1.0.16. Rounded to three decimals first: the catalog's weights are
    // decimal fractions, and their binary sum can land a hair above a whole
    // number (thirty stacks of 11 coins come to 33.000000000000014) and
    // round up one too far.
    /// <summary>A weight or capacity as the game's inventory screen shows it.</summary>
    public static int Shown(double weight) => (int)Math.Ceiling(Math.Round(weight, 3));

    // game-derived: the same method turns the total red by comparing the two
    // shown whole numbers, not the raw values. Confirmed against Valheim 1.0.16.
    public static bool IsOver(double weight, double capacity) => Shown(weight) > Shown(capacity);

    private static CarryLoad Sum(IEnumerable<(SharedItemDataDto? Shared, int Quality, int Count)> entries)
    {
        var weight = 0.0;
        var unknown = 0;

        foreach (var (shared, quality, count) in entries)
        {
            if (shared is null)
            {
                unknown++;
            }
            else
            {
                weight += shared.WeightFor(quality) * count;
            }
        }

        return new CarryLoad(weight, unknown);
    }
}
