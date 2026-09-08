using Avalonia.Controls;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>Health, stamina, eitr, active food, and two timers (time since
/// death, guardian power cooldown). No status-effect section: the character
/// file persists no active status effects of any kind, so there is nothing
/// to render.
/// <para>
/// Bars (health/stamina/eitr), timers (time-since-death, guardian power +
/// cooldown), and active food all share one <see cref="RowGroup"/>, divided
/// by <see cref="RowGroup.AddDivider"/> — independently-scoped
/// columns per section read as "weird and chaotic," including food's own
/// buttons visibly not lining up with the bars/timers above it. Food's
/// removal already rebuilds its rows from scratch (an active-food entry has
/// no stable identity to patch in place), so folding it into the shared grid
/// just means <see cref="Rebuild"/> recreates the *whole* tab on every
/// removal — the same "cheap to rebuild, nothing worth preserving mid-edit"
/// trade already made for Skills' bulk compensate.
/// </para></summary>
public sealed class VitalsTabModule : ITabModule
{
    public string Title => "Vitals";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };
        var container = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(container);

        Rebuild(container, editor, onEdited, onMessage);

        return new ScrollViewer { Content = panel };
    }

    private static void Rebuild(StackPanel container, CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        container.Children.Clear();

        var group = new RowGroup();
        AddVitalRow(group, "Health", editor, v => v.Health, v => v.MaxHealth, e => e.RestoreHealth(), onEdited);
        AddVitalRow(group, "Stamina", editor, v => v.Stamina, v => v.MaxStamina, e => e.RestoreStamina(), onEdited);
        AddVitalRow(group, "Eitr", editor, v => v.Eitr, v => v.MaxEitr, e => e.RestoreEitr(), onEdited);

        group.AddDivider();

        AddTimeSinceDeathRow(group, editor, onEdited, onMessage);
        var vitals = editor.View.Vitals;
        group.AddRow("Guardian power", vitals.GuardianPower.Length > 0 ? vitals.GuardianPower : "(none)");
        group.AddRow("Guardian power cooldown", TabRows.FormatDuration(vitals.GuardianPowerCooldown));

        group.AddDivider("Active food");

        var foods = vitals.ActiveFoods;
        for (var i = 0; i < foods.Count; i++)
        {
            var index = i;
            var food = foods[i];

            var removeButton = IconButtons.Create(IconButtons.ClearGlyph, "Removes this food effect.");
            removeButton.Click += (_, _) =>
            {
                editor.RemoveFood(index);
                // "Effect", not "food" or "item" — this is removing the
                // game's own active status effect, not deleting inventory
                // (wording chosen carefully to avoid that
                // confusion).
                onMessage($"{food.Name} effect deleted");
                Rebuild(container, editor, onEdited, onMessage);
                onEdited();
            };

            group.AddRow(food.Name, new TextBlock { Text = TabRows.FormatDuration(food.Time) }, removeButton);
        }

        container.Children.Add(group.Build());

        if (foods.Count == 0)
        {
            container.Children.Add(new TextBlock { Text = "No active food." });
        }
    }

    /// <summary>
    /// One current/max stat: a read-only <see cref="ProgressBar"/> (dragging
    /// would imply fine-tuning that doesn't apply to Health/Stamina/Eitr)
    /// plus a "current / max" text readout as the row's data, and an ↑
    /// "restore to max" button in the row's button column, disabled once
    /// already full. Center-aligned (the row's default): nothing here has
    /// its own internal top-to-bottom reading order, so centering the bar,
    /// readout, and button against each other's height reads better than
    /// flushing them all to a "top" that means nothing for a control.
    /// <paramref name="current"/>/<paramref name="max"/> select the relevant
    /// pair out of <see cref="VitalsDto"/> and <paramref name="restore"/> is
    /// the matching <see cref="CharacterEditor"/> mutator — kept as small
    /// selector functions rather than three near-identical hand-written rows.
    /// </summary>
    private static void AddVitalRow(
        RowGroup group,
        string label,
        CharacterEditor editor,
        Func<VitalsDto, float> current,
        Func<VitalsDto, float> max,
        Action<CharacterEditor> restore,
        Action onEdited)
    {
        var vitals = editor.View.Vitals;

        var data = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var bar = new ProgressBar { Minimum = 0, Maximum = max(vitals), Value = current(vitals), Width = 200, VerticalAlignment = VerticalAlignment.Center };
        var readout = new TextBlock { Text = FormatCurrentMax(current(vitals), max(vitals)), VerticalAlignment = VerticalAlignment.Center };
        data.Children.Add(bar);
        data.Children.Add(readout);

        var button = IconButtons.Create(IconButtons.MaximizeGlyph, $"Restore {label} to maximum.");
        button.IsEnabled = current(vitals) < max(vitals);
        button.Click += (_, _) =>
        {
            restore(editor);
            var updated = editor.View.Vitals;
            bar.Maximum = max(updated);
            bar.Value = current(updated);
            readout.Text = FormatCurrentMax(current(updated), max(updated));
            button.IsEnabled = current(updated) < max(updated);
            onEdited();
        };

        group.AddRow(label, data, button);
    }

    private static string FormatCurrentMax(float current, float max) => $"{current:0.#} / {max:0.#}";

    /// <summary>
    /// Time since death: a formatted duration in the data column, a ✕ reset
    /// button (disabled once already zero) in the button column, and an
    /// always-visible description explaining the "corpse run" grace mechanic
    /// — genuine context worth always showing, not the button's own
    /// mechanical "what does clicking this do" (that's the button's own
    /// tooltip now; the two aren't substitutes for each other). <c>alignTop: true</c>: the description
    /// routinely wraps to more than one line, so the row flushes everything
    /// to the description's first line rather than centering the text/button
    /// against the full multi-line block. Distinguishes the two states a
    /// reset can mean: *prolonging* an already-active window vs.
    /// *manufacturing* one from nothing, and
    /// updates live since resetting flips which of those two states applies.
    /// </summary>
    private static void AddTimeSinceDeathRow(RowGroup group, CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var text = new TextBlock { Text = FormatTimeSinceDeath(editor.View.Vitals.TimeSinceDeath) };
        var description = RowGroup.DescriptionText(FormatDeathTimerDescription(editor.View.Vitals.TimeSinceDeath));

        var button = IconButtons.Create(IconButtons.ClearGlyph, "Resets the death timer to zero.");
        button.IsEnabled = editor.View.Vitals.TimeSinceDeath != 0f;
        button.Click += (_, _) =>
        {
            // Read before resetting: whether the grace window was already
            // active decides whether the message says "started" or
            // "restarted" — same prolong-vs-manufacture distinction
            // FormatDeathTimerDescription already draws.
            var wasProtected = editor.View.Vitals.TimeSinceDeath <= DeathMechanics.HardDeathCooldownSeconds;

            editor.ResetTimeSinceDeath();
            var updated = editor.View.Vitals.TimeSinceDeath;
            text.Text = FormatTimeSinceDeath(updated);
            description.Text = FormatDeathTimerDescription(updated);
            button.IsEnabled = false;
            onMessage(wasProtected
                ? "Time since death reset, corpse run restarted"
                : "Time since death reset, corpse run started");
            onEdited();
        };

        group.AddRow("Time since death", text, button, description, alignTop: true);
    }

    /// <summary>"Never" rather than a literal ~11.6-day duration, which
    /// would read as nonsense for a fresh character — <see cref="DeathMechanics.NeverDiedSentinel"/>
    /// is the game's own sentinel value for this field.</summary>
    private static string FormatTimeSinceDeath(float seconds) =>
        seconds >= DeathMechanics.NeverDiedSentinel ? "Never" : TabRows.FormatDuration(seconds);

    /// <summary>
    /// Below <see cref="DeathMechanics.HardDeathCooldownSeconds"/>, the
    /// character is already inside the grace window, so a reset *prolongs*
    /// existing protection rather than creating it. Above it (including the
    /// "Never died" sentinel, which is far above), there is currently no
    /// protection at all, so a reset *manufactures* it from nothing — a
    /// different, more consequential action than a top-up. Kept as short as
    /// the distinction allows — explanations should fit one line
    /// where possible, though this one still wraps at the default window
    /// width — see the wider default in <see cref="MainWindow"/>.
    /// </summary>
    private static string FormatDeathTimerDescription(float secondsSinceDeath)
    {
        if (secondsSinceDeath <= DeathMechanics.HardDeathCooldownSeconds)
        {
            var remaining = DeathMechanics.HardDeathCooldownSeconds - secondsSinceDeath;
            return $"Protected: no skill loss on death right now — {TabRows.FormatDuration(remaining)} left in the grace window. Reset restarts it.";
        }

        return "Not protected: next death applies the full skill penalty. Reset creates the grace window without dying.";
    }
}
