using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Pure-math coverage for <see cref="ForgeOfPotential"/> — no catalog, no
/// corpus, no <see cref="CatalogCollection"/> tag needed.
/// </summary>
public class ForgeOfPotentialTests
{
    [Fact]
    public void ProbabilityOfReaching_is_one_at_or_under_max()
    {
        Assert.Equal(1.0, ForgeOfPotential.ProbabilityOfReaching(4, 4));
        Assert.Equal(1.0, ForgeOfPotential.ProbabilityOfReaching(1, 4));
    }

    [Fact]
    public void ProbabilityOfReaching_compounds_per_attempt_above_max()
    {
        Assert.Equal(0.65, ForgeOfPotential.ProbabilityOfReaching(5, 4), precision: 10);
        Assert.Equal(0.65 * 0.65, ForgeOfPotential.ProbabilityOfReaching(6, 4), precision: 10);
    }

    [Theory]
    [InlineData(0.65, "65.0%")]
    [InlineData(0.65 * 0.65, "42.3%")]
    public void FormatPercent_uses_one_decimal_for_ordinary_values(double probability, string expected)
    {
        Assert.Equal(expected, ForgeOfPotential.FormatPercent(probability));
    }

    [Fact]
    public void FormatPercent_grows_decimals_to_keep_a_real_digit_visible()
    {
        // 0.65^20 ~= 0.0181% — 1 decimal alone would round to "0.0%".
        var probability = Math.Pow(0.65, 20);
        Assert.Equal("0.02%", ForgeOfPotential.FormatPercent(probability));
    }

    [Fact]
    public void FormatPercent_falls_back_once_four_decimals_still_show_nothing()
    {
        // 0.65^38 ~= 7.8e-8 — needs 8 decimals to show a digit, well past
        // the 4-decimal cap.
        var probability = Math.Pow(0.65, 38);
        Assert.Equal("<0.0001%", ForgeOfPotential.FormatPercent(probability));
    }
}
