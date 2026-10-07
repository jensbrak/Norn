using Norn.Adapter;

namespace Norn.Tests;

/// <summary><see cref="GuardianPowers"/> against a synthetic lookup.</summary>
public class GuardianPowersTests
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["se_moder_name"] = "Moder",
        ["se_moder_tooltip"] = "The dragon's wings urge you on.",
        ["se_max_carryweight"] = "Max carry weight",
        ["inventory_dmgmod"] = "Damage modifier",
        ["inventory_resistant"] = "Resistant",
        ["inventory_frost"] = "Frost",
        ["item_movement_modifier"] = "Movement speed",
        ["se_ttl"] = "Effect duration",
    };

    private static string? Localize(string key) => Texts.GetValueOrDefault(key);

    [Fact]
    public void A_known_power_gets_the_games_name_flavour_text_and_buff_lines()
    {
        var power = GuardianPowers.Find("GP_Moder", Localize);

        Assert.NotNull(power);
        Assert.Equal("Moder", power.DisplayName);
        Assert.Equal(
            "The dragon's wings urge you on.\n\n"
            + "Max carry weight: +300\n"
            + "Damage modifier: Resistant VS Frost\n"
            + "Movement speed: +10%\n"
            + "Effect duration: 300",
            power.Description);
    }

    [Fact]
    public void Text_without_a_translation_stays_as_its_token()
    {
        var power = GuardianPowers.Find("GP_Eikthyr", Localize);

        Assert.NotNull(power);
        Assert.Equal("GP_Eikthyr", power.DisplayName);
        Assert.StartsWith("$se_eikthyr_tooltip\n\n$se_runstamina: -60%\n", power.Description);
        Assert.EndsWith("Effect duration: 300", power.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GP_Ashlands")]
    [InlineData("gp_moder")]
    public void A_name_the_game_has_no_power_for_is_not_found(string name)
    {
        Assert.Null(GuardianPowers.Find(name, Localize));
    }
}
