using Avalonia.Controls;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Telemetry values: scalar player stats plus the name-keyed enemy, pickup,
/// craft, pickable, food and building counters.
/// </summary>
/// <remarks>
/// <para>
/// Valheim 1.0 split all of this into ten slots, so the tab leads with a
/// selector. Two things about those slots would mislead if the selector just
/// listed them:
/// </para>
/// <list type="bullet">
/// <item><description>Slots 0–2 are not difficulties.
/// <see cref="StatisticsDifficulty.RawStats"/> counts everything
/// unconditionally, <see cref="StatisticsDifficulty.Any"/> counts everything
/// that passed the game's cheat check, and
/// <see cref="StatisticsDifficulty.Hammer"/> is never written at all. They are
/// labelled as the scopes they are.</description></item>
/// <item><description>The real tiers are cumulative, not exclusive: the game
/// credits every tier from Casual up to the one being played, so a kill on
/// Hard also lands in Default and below. Labelling a tier "Hard" flat would
/// invite the reader to check the numbers and conclude Norn is wrong, so each
/// tier says "or above".</description></item>
/// </list>
/// <para>
/// Empty slots are omitted from the selector entirely rather than shown
/// greyed: a profile always carries all ten, most characters populate four or
/// five, and an entry that can only ever say "None recorded" in six sections
/// is noise.
/// </para>
/// <para>
/// The selection is not persisted. It is per-file state — which slots even
/// exist differs between characters — so restoring "Hardcore" onto a character
/// who never played it would land on an empty view. Every open starts at
/// <see cref="StatisticsDifficulty.RawStats"/>, which is exactly what this tab
/// showed before 1.0 and the only slot guaranteed to be populated.
/// </para>
/// </remarks>
public sealed class StatisticsTabModule : ITabModule
{
    public string Title => "Statistics";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var stats = editor.View.Statistics;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };
        var body = new StackPanel { Orientation = Orientation.Vertical };

        var populated = stats.Slots.Where(slot => !slot.IsEmpty).ToList();

        // A profile with nothing recorded anywhere still has to render
        // something coherent, and RawStats is the honest thing to show: its
        // sections will each say "None recorded", which is true.
        if (populated.Count == 0)
        {
            populated = [stats.Raw];
        }

        if (populated.Count > 1)
        {
            panel.Children.Add(BuildSelector(populated, body));
        }

        Render(body, populated[0]);
        panel.Children.Add(body);

        return new ScrollViewer { Content = panel };
    }

    private static Control BuildSelector(IReadOnlyList<StatisticsSlotDto> populated, StackPanel body)
    {
        var combo = new ComboBox
        {
            ItemsSource = populated.Select(DescribeSlot).ToList(),
            SelectedIndex = 0,
            MinWidth = 260,
        };

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < populated.Count)
            {
                Render(body, populated[combo.SelectedIndex]);
            }
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 0, 12),
        };
        row.Children.Add(new TextBlock { Text = "Recorded for:", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(combo);
        return row;
    }

    /// <summary>
    /// How a slot is named in the selector — see the class remarks for why
    /// these are not just the enum names.
    /// </summary>
    private static string DescribeSlot(StatisticsSlotDto slot) => slot.Difficulty switch
    {
        StatisticsDifficulty.RawStats => "Everything (including cheated play)",
        StatisticsDifficulty.Any => "Everything that counted",
        StatisticsDifficulty.Hammer => "Hammer",
        StatisticsDifficulty.Casual => "Casual or above",
        StatisticsDifficulty.VeryEasy => "Very easy or above",
        StatisticsDifficulty.Easy => "Easy or above",
        StatisticsDifficulty.Default => "Default or above",
        StatisticsDifficulty.Hard => "Hard or above",
        StatisticsDifficulty.VeryHard => "Very hard or above",
        StatisticsDifficulty.Hardcore => "Hardcore",
        _ => slot.Difficulty.ToString(),
    };

    private static void Render(StackPanel body, StatisticsSlotDto slot)
    {
        body.Children.Clear();

        var group = new RowGroup(rowSpacing: 4);

        AddSection(group, "Player stats", slot.PlayerStats, isFirst: true);

        // Only the total is shown unconditionally. The four attributed
        // breakdowns are shown when they hold anything — they do not sum to
        // the total (a kill counts toward it unconditionally but toward a
        // category only when the game attributes one), so presenting all five
        // as equals would invite exactly the wrong arithmetic.
        AddSection(group, "Enemy kills", KillsFor(slot, KillModifier.MixedAndTotal));

        foreach (var modifier in new[] { KillModifier.Melee, KillModifier.Ranged, KillModifier.Magic, KillModifier.Unarmed })
        {
            var kills = KillsFor(slot, modifier);
            if (kills.Count > 0)
            {
                AddSection(group, $"Enemy kills — {modifier.ToString().ToLowerInvariant()}", kills);
            }
        }

        AddSection(group, "Item pickup stats", slot.ItemPickupStats);
        AddSection(group, "Item craft stats", slot.ItemCraftStats);
        AddSection(group, "Foraged", slot.PickableStats);
        AddSection(group, "Food eaten", slot.FoodEatenStats);
        AddSection(group, "Pieces built", slot.PiecesPlacedStats);

        body.Children.Add(group.Build());
    }

    private static IReadOnlyList<StatDto> KillsFor(StatisticsSlotDto slot, KillModifier modifier)
        => (int)modifier < slot.EnemyStats.Count ? slot.EnemyStats[(int)modifier] : [];

    private static void AddSection(RowGroup group, string title, IReadOnlyList<StatDto> values, bool isFirst = false)
    {
        if (!isFirst)
        {
            group.AddDivider();
        }

        group.AddSectionHeader(title);

        if (values.Count == 0)
        {
            group.AddRow(null, new TextBlock { Text = "None recorded." });
            return;
        }

        foreach (var stat in values.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            group.AddRow(stat.Name, stat.Value.ToString("0.##"));
        }
    }
}
