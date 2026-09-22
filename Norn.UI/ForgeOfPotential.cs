namespace Norn.UI;

/// <summary>
/// Facts about Valheim 1.0's "Forge of Potential" refinement mechanic that
/// Norn needs only to describe an over-max item to a user. Presentation-only
/// (feeds exactly one control, the Inventory tile's over-max tooltip line),
/// so this lives in <c>Norn.UI</c> rather than <c>Norn.Adapter</c>: nothing
/// on the read or write path needs it, only display.
/// </summary>
public static class ForgeOfPotential
{
    // game-derived: CraftingStation upgrader idols' m_upgradeChance (flat
    // across all 16 idol SharedData assets — not tiered by idol), confirmed
    // 1.0.15.
    private const double SuccessChance = 0.65;

    /// <summary>Chance of reaching <paramref name="quality"/> from
    /// <paramref name="maxQuality"/> via that many consecutive, independent
    /// Forge attempts — 1.0 when already at or under max (zero attempts
    /// needed).</summary>
    public static double ProbabilityOfReaching(int quality, int maxQuality) =>
        Math.Pow(SuccessChance, Math.Max(0, quality - maxQuality));

    /// <summary>
    /// Percent-formats a probability with adaptive precision: 1 decimal
    /// normally (<c>65.0%</c>), growing up to 4 decimals to keep a real
    /// digit visible for small probabilities, then falling back to
    /// <c>&lt;0.0001%</c> beyond that rather than an ever-lengthening
    /// string.
    /// </summary>
    public static string FormatPercent(double probability)
    {
        var pct = probability * 100;
        for (var decimals = 1; decimals <= 4; decimals++)
        {
            var rounded = Math.Round(pct, decimals);
            if (rounded > 0)
            {
                return rounded.ToString("F" + decimals) + "%";
            }
        }

        return "<0.0001%";
    }
}
