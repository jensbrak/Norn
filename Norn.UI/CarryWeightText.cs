using Avalonia.Controls;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// How carry weight reads on screen, shared by the Inventory tab's toolbar and
/// the two add pickers so all three say the same thing the same way. The
/// numbers themselves are <see cref="CarryWeight"/>'s.
/// </summary>
public static class CarryWeightText
{
    private const string Label = "Weight:";

    // Appended whenever the catalog couldn't resolve something that was
    // summed: the figure is then a floor, not the real total.
    private const string UnknownMarker = " (+?)";

    /// <summary>"212/450" — total over capacity, in the game's own whole-number form.</summary>
    public static string Total(CarryLoad load, double capacity) =>
        $"{CarryWeight.Shown(load.Weight)}/{CarryWeight.Shown(capacity)}{(load.UnknownItems > 0 ? UnknownMarker : "")}";

    /// <summary>
    /// "+100.0 → 312/450" — what an add weighs and the total it leads to. The
    /// added weight keeps a decimal: rounded up like the total, five coins
    /// would read as +1 and the figures wouldn't add up.
    /// </summary>
    public static string Addition(CarryLoad added, CarryLoad carried, double capacity) =>
        $"+{added.Weight:0.0} → {Total(carried + added, capacity)}";

    /// <summary>The toolbar readout's tooltip: what the two numbers are, where
    /// the capacity comes from, and what the total leaves out.</summary>
    public static string Explain(CarryLoad load, bool megingjordEquipped)
    {
        var capacity = megingjordEquipped
            ? $"Capacity: {CarryWeight.BaseCapacity:0} base + {CarryWeight.MegingjordBonus:0} Megingjord."
            : $"Capacity: {CarryWeight.BaseCapacity:0} base.";

        var lines = new List<string> { "Carried weight / carry capacity.", capacity };
        if (load.UnknownItems > 0)
        {
            lines.Add($"{TabRows.Pluralize(load.UnknownItems, "item")} of unknown weight not counted.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>Shows the inventory's current total on <paramref name="target"/>.</summary>
    internal static void ShowTotal(TextBlock target, CarryLoad load, double capacity) =>
        Show(target, Total(load, capacity), CarryWeight.IsOver(load.Weight, capacity));

    /// <summary>Shows what adding <paramref name="added"/> would lead to, or
    /// just the bare label while there is nothing to add.</summary>
    internal static void ShowAddition(TextBlock target, CarryLoad? added, CarryLoad carried, double capacity)
    {
        if (added is not { } load)
        {
            Show(target, "", over: false);
            return;
        }

        Show(target, Addition(load, carried, capacity), CarryWeight.IsOver((carried + load).Weight, capacity));
    }

    // Red when over capacity, as in the game — informational only, nothing is
    // ever blocked on it.
    private static void Show(TextBlock target, string value, bool over)
    {
        target.Text = value.Length == 0 ? Label : $"{Label} {value}";

        if (over)
        {
            target.Foreground = Brushes.IndianRed;
        }
        else
        {
            target.ClearValue(TextBlock.ForegroundProperty);
        }
    }
}
