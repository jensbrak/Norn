using Avalonia.Controls;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// One slider per discovered skill (roadmap item 6), plus a bulk
/// "compensate for death" control. Shows only skills already present in
/// <see cref="SkillsDto"/> — a skill the character has never raised has no
/// entry to edit, and originating one from scratch would let the editor
/// grant a skill the game itself only ever hands out through play. That's
/// a different, bigger feature (deliberately out of scope this pass) and
/// would need its own gated opt-in, not a slider
/// starting at 0 indistinguishable from a skill that's merely untrained.
/// <para>
/// The compensate row and the skill list share one <see cref="RowGroup"/>,
/// divided by an unlabeled <see cref="RowGroup.AddDivider"/>, so both
/// sections share tab-stops — independently-scoped columns
/// per section read as "weird and chaotic" even here, despite the compensate
/// row's wider "Apply" button forcing some unused column-3 width onto every
/// skill row below it. The XP-progress readout stays part of each skill
/// row's data (dimmed), not moved to the description column — description is
/// reserved for explanatory prose, not a derived readout.
/// </para></summary>
/// <remarks>
/// The skill-leveling math itself (<see cref="SkillProgression"/>) lives in
/// <c>Norn.Adapter</c>, not here — it's domain knowledge shared with the
/// write path, not UI-presentation-only. Carries a <c>game-derived:</c>
/// tag naming its source, which is what to re-check on a future game patch.
/// </remarks>
public sealed class SkillsTabModule : ITabModule
{
    public string Title => "Skills";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };
        var container = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(container);

        Rebuild(container, editor, onEdited, onMessage, SkillProgression.DefaultCompensatePercent);

        return new ScrollViewer { Content = panel };
    }

    /// <summary>
    /// Clears and rebuilds the whole tab — compensate row included — from
    /// the editor's current list. The compensate row's own slider never
    /// needs its value read back from anywhere else (bulk compensate doesn't
    /// change it), so rebuilding it too, rather than trying to keep it as a
    /// standalone survivor while only the list below churns, costs nothing —
    /// it just needs re-seeding at <paramref name="compensatePercent"/>
    /// (the value the user had dialed in, captured by the Apply handler)
    /// rather than snapping back to the default every time.
    /// </summary>
    private static void Rebuild(StackPanel container, CharacterEditor editor, Action onEdited, Action<string> onMessage, float compensatePercent)
    {
        container.Children.Clear();

        var group = new RowGroup();
        AddCompensateRow(group, editor, onEdited, onMessage, container, compensatePercent);
        group.AddDivider();

        var skills = editor.View.Skills.Skills.OrderBy(s => s.Type, StringComparer.Ordinal).ToList();
        foreach (var skill in skills)
        {
            AddSkillRow(group, skill.Type, editor, onEdited);
        }

        container.Children.Add(group.Build());

        if (skills.Count == 0)
        {
            container.Children.Add(new TextBlock { Text = "No skills recorded." });
        }
    }

    /// <summary>
    /// One skill: name, a 0-100 slider snapped to whole levels, the bare
    /// truncated level value (no "Level:" text — just the number),
    /// and the accumulator expressed as percent-to-next-level plus its raw
    /// numerator/denominator, in dimmed text alongside it — all part of the
    /// row's data column, not split across data/description (see the type
    /// doc's grouping note).
    /// <para>
    /// Snapping and truncating are deliberate, not "fractional doesn't
    /// matter" — it does: <c>Skills.Skill.m_level</c> only moves in whole
    /// steps during ordinary play (<c>Skill.Raise</c> adds every gain to
    /// <c>m_accumulator</c> alone, incrementing <c>m_level</c> by exactly 1
    /// once <see cref="SkillProgression.GetNextLevelRequirement"/> is met), but death
    /// (<c>LowerAllSkills</c>), skill-cap rebalancing, and the console cheat
    /// command all write fractional levels directly, so a played, once-dead
    /// character routinely has them — confirmed in the decompiled source
    /// against 0.221.10/0.221.4. The
    /// slider still snaps because dragging it is a deliberate overwrite of
    /// whatever was there (same reasoning the bulk compensate action
    /// doesn't snap — it's deliberately *preserving* the fraction it's
    /// scaling, not setting a new target). Truncating the readout (not
    /// rounding) matches the game's own display (<c>SkillsDialog.Setup</c>
    /// casts <c>(int)m_level</c>) — rounding would show a different number
    /// than the player's own game does for the same unedited value.
    /// </para>
    /// </summary>
    private static void AddSkillRow(RowGroup group, string type, CharacterEditor editor, Action onEdited)
    {
        var skill = editor.View.Skills.Skills.First(s => s.Type == type);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = SkillProgression.MaxLevel,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Value = Math.Floor(skill.Level),
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var readout = new TextBlock { Text = FormatLevel(skill.Level), Width = 28, VerticalAlignment = VerticalAlignment.Center };
        var xpReadout = new TextBlock { Text = FormatProgress(skill.Level, skill.Accumulator), Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };

        slider.ValueChanged += (_, _) =>
        {
            editor.SetSkillLevel(type, (float)slider.Value);
            var updated = editor.View.Skills.Skills.First(s => s.Type == type);
            readout.Text = FormatLevel(updated.Level);
            xpReadout.Text = FormatProgress(updated.Level, updated.Accumulator);
            onEdited();
        };

        var data = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        data.Children.Add(slider);
        data.Children.Add(readout);
        data.Children.Add(xpReadout);

        group.AddRow(type, data);
    }

    /// <summary>
    /// The bulk "compensate for death" control: a percent slider (seeded at
    /// <paramref name="seedPercent"/>) and a button that multiplies every
    /// present skill's level by it. Continuous, not snapped to whole percent
    /// — the meaningful default (≈5.263%) isn't a whole number, and unlike
    /// the per-skill level, precision here is the point. The explanation of
    /// what the button does lives in this row's description column rather
    /// than as a heading above it — the row's own label ("Raise all skills
    /// by") already says what the control does; the description supplies
    /// the "why." <c>alignTop: true</c>: the description wraps to more than
    /// one line at the default window width, so the slider and the "Apply"
    /// button both flush to its first line rather than centering against
    /// the full block — <c>Apply</c> in particular needs an explicit
    /// alignment here since, unlike <see cref="IconButtons.Create"/>'s
    /// glyph buttons, it has no fixed <c>Height</c> of its own and would
    /// otherwise stretch to fill the row.
    /// </summary>
    private static void AddCompensateRow(RowGroup group, CharacterEditor editor, Action onEdited, Action<string> onMessage, StackPanel container, float seedPercent)
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = seedPercent, Width = 220, VerticalAlignment = VerticalAlignment.Top };
        var readout = new TextBlock { Text = FormatPercent(seedPercent), Width = 56, VerticalAlignment = VerticalAlignment.Top };
        slider.ValueChanged += (_, _) => readout.Text = FormatPercent((float)slider.Value);

        var data = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        data.Children.Add(slider);
        data.Children.Add(readout);

        var button = new Button { Content = "Apply" };
        button.Click += (_, _) =>
        {
            var percent = (float)slider.Value;
            var count = editor.View.Skills.Skills.Count;
            editor.CompensateSkillsForDeath(percent);
            onMessage($"{count} {(count == 1 ? "skill" : "skills")} raised by {FormatPercent(percent)}");
            Rebuild(container, editor, onEdited, onMessage, percent);
            onEdited();
        };

        group.AddRow(
            "Raise all skills by",
            data,
            button,
            RowGroup.DescriptionText("Multiplies every skill's level by this percent, matching how the game removes levels on death."),
            alignTop: true);
    }

    /// <summary>Truncates, matching <c>SkillsDialog.Setup</c>'s own <c>(int)m_level</c> cast.</summary>
    private static string FormatLevel(float level) => $"{(int)level}";

    /// <summary>
    /// "n.nn% (x.xx / y.yy)" — percent-to-next-level first (what a player
    /// actually wants to know, matching <c>Skill.GetLevelPercentage</c>'s
    /// own framing), raw accumulator/requirement in parens for verification.
    /// Every number fixed to 2 decimal places regardless of whether it's a
    /// whole number — a varying decimal count reads as messier than
    /// always showing the max (2) seen among these values. 0% at level 100:
    /// there's no next level to progress toward, matching
    /// <c>GetLevelPercentage</c>'s own special case rather than dividing by
    /// a requirement that no longer means anything.
    /// </summary>
    private static string FormatProgress(float level, float accumulator)
    {
        if (level >= SkillProgression.MaxLevel)
        {
            return "0.00%";
        }

        var requirement = SkillProgression.GetNextLevelRequirement(level);
        var percent = Math.Clamp(accumulator / requirement, 0f, 1f) * 100f;
        return $"{percent:0.00}% ({accumulator:0.00} / {requirement:0.00})";
    }

    private static string FormatPercent(float percent) => $"{percent:0.###}%";
}
