using System.Text.RegularExpressions;

namespace Norn.Adapter;

/// <summary>
/// One guardian power as the game presents it. <see cref="Name"/> is the
/// string the save stores. <see cref="Description"/> is the text the game
/// shows at the power's stone: its flavour text, then one line per buff.
/// Anything the localization lookup misses stays as its raw token.
/// </summary>
public sealed record GuardianPower(string Name, string DisplayName, string Description);

/// <summary>
/// The guardian ("Forsaken") powers a character can have selected. Feature
/// code, not <c>GameCore</c> — the save holds only the selected power's name
/// and its cooldown.
/// </summary>
public static partial class GuardianPowers
{
    // game-derived: the seven GP_* SE_Stats assets (Assets/MonoBehaviour),
    // one per BossStone_* prefab's ItemStand.m_guardianPower. The save stores
    // the asset's object name (ItemStand.DelayedPowerActivation passes
    // m_guardianPower.name); the game shows the localized m_name
    // ($<stem>_name) and SE_Stats.GetTooltipString: m_tooltip
    // ($<stem>_tooltip), a blank line, then a line per non-default stat
    // field. The effect lines here are that method's output for each asset's
    // values, tokens left for the localization lookup. Moder's tailwind is
    // StatusEffect.m_attributes (SailingPower), which the game prints no line
    // for. In boss order. Confirmed against Valheim 1.0.16.
    private static readonly (string Name, string TokenStem, string[] Effects)[] Known =
    [
        ("GP_Eikthyr", "se_eikthyr",
        [
            "$se_runstamina: -60%",
            "$se_jumpstamina: -60%",
            "$se_swimstamina: -60%",
        ]),
        ("GP_TheElder", "se_theelder",
        [
            "$se_healthregen: +30%",
            "$inventory_chop: +60%",
            "$inventory_pickaxe: +60%",
        ]),
        ("GP_Bonemass", "se_bonemass",
        [
            "$inventory_dmgmod: $inventory_slightlyresistant VS $inventory_blunt",
            "$inventory_dmgmod: $inventory_slightlyresistant VS $inventory_slash",
            "$inventory_dmgmod: $inventory_slightlyresistant VS $inventory_pierce",
            "$se_blockstamina: -100%",
            "$se_blockstaminaflat_minus: +5",
        ]),
        ("GP_Moder", "se_moder",
        [
            "$se_max_carryweight: +300",
            "$inventory_dmgmod: $inventory_resistant VS $inventory_frost",
            "$item_movement_modifier: +10%",
        ]),
        ("GP_Yagluth", "se_yagluth",
        [
            "$inventory_dmgmod: $inventory_resistant VS $inventory_lightning",
            "$skill_farming +25",
            "$inventory_blunt: +10%",
            "$inventory_slash: +10%",
            "$inventory_pierce: +10%",
            "$inventory_chop: +10%",
            "$inventory_pickaxe: +10%",
            "$inventory_fire: +10%",
            "$inventory_frost: +10%",
            "$inventory_lightning: +10%",
            "$inventory_poison: +10%",
            "$inventory_spirit: +10%",
        ]),
        ("GP_Queen", "se_queen",
        [
            "$se_eitrregen: +100%",
            "$inventory_dmgmod: $inventory_resistant VS $inventory_poison",
            "$se_sneakstamina: -100%",
        ]),
        ("GP_Fader", "se_fader",
        [
            "$inventory_dmgmod: $inventory_resistant VS $inventory_fire",
            "$se_adrenaline: +100%",
            "$se_stagger: +50%",
        ]),
    ];

    // game-derived: StatusEffect.m_ttl, 300 on all seven assets, which
    // SE_Stats.GetTooltipString prints last. Confirmed against Valheim 1.0.16.
    private const string DurationLine = "$se_ttl: 300";

    /// <summary>Every power's save name, in boss order.</summary>
    public static IReadOnlyList<string> Names { get; } = Known.Select(power => power.Name).ToList();

    /// <summary>
    /// The power the save calls <paramref name="name"/>, or <c>null</c> for a
    /// name the game has no power for — which the game itself treats as no
    /// power at all.
    /// </summary>
    public static GuardianPower? Find(string name, Func<string, string?> localize)
    {
        foreach (var (knownName, stem, effects) in Known)
        {
            if (knownName != name)
            {
                continue;
            }

            var lines = string.Join("\n", effects.Append(DurationLine));
            var text = $"${stem}_tooltip\n\n{lines}";

            return new GuardianPower(
                name,
                localize(stem + "_name") ?? name,
                Token().Replace(text, token => localize(token.Value[1..]) ?? token.Value));
        }

        return null;
    }

    [GeneratedRegex(@"\$\w+")]
    private static partial Regex Token();
}
