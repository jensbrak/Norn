using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>
/// Shared row-building helpers for tab modules: <see cref="RowGroup"/> is the
/// row/column primitive (including <see cref="RowGroup.AddDivider"/>, the
/// boundary between sections), plus small formatting helpers with no layout
/// opinion of their own.
/// </summary>
internal static class TabRows
{
    /// <summary>
    /// Renders a bool as "Yes"/"No" rather than C#'s default "True"/"False".
    /// </summary>
    public static string FormatBool(bool value) => value ? "Yes" : "No";

    /// <summary>"ConfirmDeletion" → "Confirm deletion": space before each
    /// capital, lowercase everything but the first letter — sentence case,
    /// matching every other row label in the app (General's "Player name:",
    /// "Used cheats:"), not Title Case. Promoted out of <c>SettingsWindow</c>
    /// (its original, still only other consumer) once the Add Item picker's
    /// category combo needed the identical PascalCase-to-sentence-case
    /// conversion for <see cref="Norn.Adapter.ItemType"/> names — the same
    /// "would these two ever diverge" test as any other shared helper,
    /// answer no.</summary>
    public static string Humanize(string name)
    {
        var spaced = Regex.Replace(name, "(?<!^)([A-Z])", " $1");
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    /// <summary>
    /// Formats a real-seconds duration as "X days, Y hours, Z minutes and
    /// W seconds (Ns)" — only units that are actually nonzero are shown (a
    /// cooldown that never reaches an hour renders as e.g. "12 minutes and
    /// 4 seconds (724s)", not "0 days, 0 hours, 12 minutes..."), with the
    /// exact raw seconds always shown in parentheses regardless. Shared
    /// across every Vitals timer (time since death, guardian power
    /// cooldown, active-food duration) rather than hand-rolled per field —
    /// a general formatter costs nothing extra even for a
    /// field (cooldown) that in practice never reaches days.
    /// </summary>
    public static string FormatDuration(float totalSeconds)
    {
        var whole = (long)Math.Floor(totalSeconds);
        var days = whole / 86400;
        var hours = whole % 86400 / 3600;
        var minutes = whole % 3600 / 60;
        var seconds = whole % 60;

        var parts = new List<string>();
        if (days > 0) parts.Add(Pluralize(days, "day"));
        if (hours > 0) parts.Add(Pluralize(hours, "hour"));
        if (minutes > 0) parts.Add(Pluralize(minutes, "minute"));
        if (seconds > 0 || parts.Count == 0) parts.Add(Pluralize(seconds, "second"));

        var joined = parts.Count == 1
            ? parts[0]
            : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];

        return $"{joined} ({totalSeconds:0.#}s)";
    }

    /// <summary>
    /// Closed-by-default reveal for a long, raw-string list — an
    /// <see cref="Expander"/> rather than a hover tooltip, since tooltips
    /// can't scroll and aren't interactive, both of which matter once a
    /// per-entry action lands inside one in a later pass. Promoted out of
    /// <c>UnlockablesTabModule</c> (its original sole consumer) once
    /// <c>GeneralTabModule</c>'s "Known commands" row needed
    /// the identical shape — the same "would these two call sites ever
    /// really diverge" test any shared helper in this codebase gets, answer
    /// no.
    /// </summary>
    public static Expander BuildExpander(IReadOnlyList<string> values, string header = "Show items")
    {
        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        foreach (var value in values)
        {
            list.Children.Add(new TextBlock { Text = value });
        }

        return new Expander
        {
            Header = header,
            Content = list,
            IsExpanded = false,
        };
    }

    /// <summary>"{value} {unit}" / "{value} {unit}s" — internal, not
    /// private: also used by <see cref="WorldMapWindow"/>'s pin-count
    /// title (own/received pin counts), which is pure count-of-things
    /// pluralization with no time-span meaning at all. Kept as one shared
    /// helper rather than two near-identical private copies — the rule
    /// itself ("add an s unless it's exactly 1") carries no domain
    /// knowledge either caller needs to own separately.</summary>
    internal static string Pluralize(long value, string unit) => $"{value} {unit}{(value == 1 ? "" : "s")}";

    /// <summary>
    /// A self-contained list of (primary, secondary) pairs, one per line,
    /// each in its own aligned column — first built for Unlockables' Known
    /// stations (name + level), but general-purpose: any list
    /// that's genuinely two-column data rather than a single value can
    /// reuse this instead of a bespoke per-field layout. Deliberately its
    /// own small <see cref="Grid"/>, not built from <see cref="RowGroup.AddRow"/>'s
    /// label column — a bold, colon-suffixed label reads as a form field
    /// (that's the point of <c>AddRow</c>'s own label), which is wrong for
    /// a list entry that just happens to carry two values instead of one.
    /// Returns one composite <see cref="Control"/>, so it drops into a
    /// single <see cref="RowGroup.AddRow"/> data cell (always-visible) or
    /// an <see cref="Expander"/>'s <c>Content</c> (collapsed) the same way
    /// either shape already builds a flat single-value list.
    /// </summary>
    public static Control BuildPairedList(IEnumerable<(string Primary, string Secondary)> pairs)
    {
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var row = 0;
        foreach (var (primary, secondary) in pairs)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var primaryText = new TextBlock { Text = primary };
            Grid.SetRow(primaryText, row);
            Grid.SetColumn(primaryText, 0);
            grid.Children.Add(primaryText);

            var secondaryText = new TextBlock { Text = secondary, Opacity = 0.7 };
            Grid.SetRow(secondaryText, row);
            Grid.SetColumn(secondaryText, 1);
            grid.Children.Add(secondaryText);

            row++;
        }

        return grid;
    }
}

/// <summary>
/// One visually-aligned run of rows: label / data / button / description,
/// left-aligned, each column <c>Auto</c>-sized to the widest content *in
/// this <see cref="RowGroup"/>* (except description, which takes the
/// remaining width and wraps) — not a hardcoded per-tab guess, which is what
/// let Vitals' "Guardian power cooldown" label overflow its column in the
/// first place. Any of label/button/description may be omitted per row; a
/// column nothing in the group ever uses collapses to zero width.
/// </summary>
/// <remarks>
/// Column sizing is shared by every row added to the *same* <see cref="RowGroup"/>
/// instance, including across an internal <see cref="AddDivider"/>
/// boundary: a tab that wants its sections to
/// share tab-stops (General, Vitals' bars/timers, Skills, Statistics) uses
/// one <see cref="RowGroup"/> for the whole tab and calls
/// <see cref="AddDivider"/> between sections. A tab whose sections
/// genuinely shouldn't constrain each other's width — Vitals' active-food
/// list, which is independently rebuilt on every removal and unrelated in
/// shape to the field rows above it; Worlds' per-world blocks, not
/// requested to align across worlds — uses a separate <see cref="RowGroup"/>
/// per section instead. Both are the same primitive; the choice of one
/// instance vs. several *is* the alignment-scope decision, made once at the
/// call site rather than baked into the type.
/// </remarks>
internal sealed class RowGroup
{
    private readonly Grid _grid;
    private int _row;

    /// <param name="rowSpacing">
    /// Vertical gap between rows. Defaults to 8, matching every group that
    /// mixes in a slider/progress-bar/button — those need the breathing
    /// room. Statistics passes a tighter value: its rows are plain text with
    /// nothing needing room, and that tab is deliberately dense
    /// so its 100+ rows stay easy to scan.
    /// </param>
    public RowGroup(double rowSpacing = 8)
    {
        _grid = new Grid { ColumnSpacing = 8, RowSpacing = rowSpacing };
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // label
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // data
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // button
        _grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star))); // description
    }

    /// <summary>
    /// The general row shape. <paramref name="data"/> may itself be a panel
    /// stacking several sub-rows (General's color sliders: a slider row plus
    /// a swatch/readout row, both inside this one cell) — the group's own
    /// row/column grid never needs to know about that internal structure.
    /// <paramref name="description"/> is always-visible explanatory text,
    /// replacing an in-tab tooltip — not a restatement of the data, and not
    /// where secondary/derived readouts belong (e.g. Skills' XP
    /// percent-to-next-level stays part of <paramref name="data"/>, dimmed,
    /// not moved here). Takes a <see cref="Control"/>
    /// rather than a bare string so a caller that needs to update the text
    /// later (Vitals' time-since-death row, whose explanation depends on
    /// live state) can build it via <see cref="DescriptionText"/>, keep the
    /// reference, and mutate <see cref="TextBlock.Text"/> directly.
    /// <para>
    /// <paramref name="alignTop"/> picks the row's vertical alignment for
    /// every cell RowGroup places directly (label/data/button/description) —
    /// deliberately not inferred from whether a description is present, since
    /// that guess is wrong in both directions: General's color rows need Top
    /// with no description at all (their data is genuinely multi-part —
    /// a slider sub-row, then a swatch sub-row — and Top keeps both flush
    /// with the label), while a row with a short, likely-one-line
    /// description reads better Center-aligned against its button. The rule
    /// that actually holds: Top when a row's extra height comes from content
    /// with its own internal top-to-bottom order (wrapped prose, stacked
    /// sub-rows); Center when it merely comes from a control that's bigger
    /// than a line of text (a button's click-target, a slider's drag-target)
    /// with nothing to "start reading" from. The tab module, which knows
    /// which case a given row is, decides — RowGroup only applies it
    /// consistently once decided. A composite <paramref name="data"/> panel
    /// (e.g. Vitals' progress-bar-plus-readout) has this alignment forced
    /// onto its own outer container; any control nested *inside* it still
    /// needs its own matching alignment set by the caller, since RowGroup
    /// can't reach inside an opaque data panel.
    /// </para>
    /// </summary>
    public void AddRow(string? label, Control data, Control? button = null, Control? description = null, bool alignTop = false)
    {
        var align = alignTop ? VerticalAlignment.Top : VerticalAlignment.Center;
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        if (label is not null)
        {
            Place(new TextBlock { Text = label + ":", FontWeight = FontWeight.Bold, VerticalAlignment = align }, 0);
        }

        data.VerticalAlignment = align;
        if (label is null)
        {
            // No label to anchor column 0 — start the data flush there too
            // (spanning into column 1) instead of leaving it at column 1's
            // position, which reads as an unintended indent relative to
            // this row's own section header (Unlockables' list rows,
            // Statistics' "None recorded." fallback) — a real cross-tab
            // inconsistency once Worlds/Unlockables/
            // Statistics started sharing the same header shape, not a
            // deliberate "indent under a header" style worth keeping.
            Place(data, 0);
            Grid.SetColumnSpan(data, 2);
        }
        else
        {
            Place(data, 1);
        }

        if (button is not null)
        {
            button.VerticalAlignment = align;
            // Right-aligned, not left/default — same reasoning and same
            // fix as AddSectionHeader's own button param (see its doc
            // comment): the button column's width is driven by the widest
            // button-cell content across the whole group (Worlds' "Has map
            // data" row packs two buttons into it), so a lone button
            // defaults to that column's left edge instead of lining up
            // with a same-column row that has more than one. Degenerates to
            // the same position a plain default would give when nothing
            // else in the group needs the extra width, so this is safe even
            // for groups where every row has at most one button.
            button.HorizontalAlignment = HorizontalAlignment.Right;
            Place(button, 2);
        }

        if (description is not null)
        {
            description.VerticalAlignment = align;
            Place(description, 3);
        }

        _row++;
    }

    /// <summary>Plain read-only label:value text — the common case.</summary>
    public void AddRow(string label, string value) => AddRow(label, TextCell(value));

    public void AddRow(string label, bool value) => AddRow(label, TabRows.FormatBool(value));

    /// <summary>
    /// A pure boundary between two sections within this group: an optional
    /// bold title followed by a horizontal line, spanning all four columns —
    /// nothing else. <paramref name="title"/> is omitted where the grouping
    /// is self-evident from the rows themselves (Vitals' health/stamina/eitr
    /// trio); named where it isn't (Vitals' "Active food").
    /// <para>
    /// <paramref name="count"/> appends "(N)" to the title.
    /// </para>
    /// <para>
    /// Every row in the group shares one uniform <c>RowSpacing</c>
    /// (constructor param), so a divider landing right after a plain-text
    /// row read as no more separated from the previous content than any two
    /// ordinary items in the same list — boldness and the separator line
    /// were the only signal. A divider that isn't the group's first row gets
    /// extra top margin on top of that shared spacing, so starting a new
    /// section reads as a real break; the first divider gets none, since the
    /// panel's own outer margin already opens the group.
    /// </para>
    /// <para>
    /// <b>Never a section header.</b> Worlds/Unlockables/Statistics used to
    /// route their named sections through here (a titled divider standing in
    /// for a header) — reverted, see <see cref="AddSectionHeader"/>'s doc
    /// comment for why that broke down. This method carries no button
    /// parameter and never will; a control that names an action belongs on a
    /// header, never floated on a line whose only job is separating two
    /// things.
    /// </para>
    /// </summary>
    public void AddDivider(string? title = null, int? count = null)
    {
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = _row == 0 ? default : new Thickness(0, 12, 0, 0),
        };
        if (title is not null)
        {
            var text = count is null ? title : $"{title} ({count})";
            stack.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.Bold });
        }

        stack.Children.Add(new Separator());

        Grid.SetRow(stack, _row);
        Grid.SetColumn(stack, 0);
        Grid.SetColumnSpan(stack, 4);
        _grid.Children.Add(stack);

        _row++;
    }

    /// <summary>
    /// A named section's own heading: bold title, optional "(N)" count
    /// suffix, optional action button in the group's real button column
    /// (column 2 — the same one <see cref="AddRow"/> places every ordinary
    /// row's button in) — and deliberately <b>no line</b>.
    /// <para>
    /// Split out of <see cref="AddDivider"/>, 2026-08-26, once Worlds
    /// needed a "Remove world" button on
    /// its "World N" title and the two things a titled divider had always
    /// quietly conflated — "this line separates two blocks" and "this text
    /// names the section that follows" — turned out not to be the same
    /// concept at all. The first fix tried keeping the button on
    /// <see cref="AddDivider"/> and docking it against the divider's own
    /// four-column width; that landed the button under the far edge of the
    /// wide description column, nowhere near where every other button in the
    /// tab sits ("very much to the right... almost not 'visible'").
    /// The second fix placed it in the real button column
    /// instead, which fixed the alignment but not the underlying complaint:
    /// the read, once the button worked, was that a divider was
    /// never the right primitive for a named, potentially-actionable
    /// section to begin with — General/Vitals/Skills use dividers correctly
    /// (pure breaks, nothing interactive ever sits on one), while
    /// Worlds/Unlockables/Statistics were all using a titled divider to
    /// *be* the section boundary, three slightly different ways, which is
    /// what made the button placement ambiguous in the first place. A
    /// header names a section; a divider separates two things. Composing
    /// them explicitly — <see cref="AddDivider"/> before a header, skipped
    /// for the very first section since there is nothing to divide there —
    /// replaced all three tabs' titled-divider sections in the
    /// same pass, not just Worlds, since the same conflation existed in all
    /// three.
    /// </para>
    /// <para>
    /// No label/explanation needed for a lone ✕ next to a section's own
    /// name — "remove [the thing named right here]" doesn't need spelling
    /// out the way a same-glyph action buried in an unrelated row would.
    /// </para>
    /// </summary>
    public void AddSectionHeader(string title, int? count = null, Control? button = null)
    {
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var text = count is null ? title : $"{title} ({count})";
        var titleBlock = new TextBlock { Text = text, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(titleBlock, _row);
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumnSpan(titleBlock, button is null ? 4 : 2); // stop short of the button's own column when there is one
        _grid.Children.Add(titleBlock);

        if (button is not null)
        {
            button.VerticalAlignment = VerticalAlignment.Center;
            // Right-aligned, not left/default: the button column's width is
            // driven by the widest button-cell content across the whole
            // group (Worlds' "Has map data" row packs two buttons — »
            // then ✕ — into that column), and a lone button defaults to
            // that column's left edge, landing under » instead of under ✕
            // (found live: "X buttons needs to be above each other"). Right
            // alignment degenerates to the same position a plain Left
            // default would give when the column is exactly this button's
            // own width (no wider sibling in the group), so this is safe
            // even when nothing else in the group needs the extra space.
            button.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(button, _row);
            Grid.SetColumn(button, 2);
            _grid.Children.Add(button);
        }

        _row++;
    }

    /// <summary>
    /// Builds the description column's standard styling — italic, wrapping
    /// within whatever width the column's <c>Star</c> sizing leaves free.
    /// Kept separate from <see cref="AddRow"/> itself so a caller that needs
    /// to mutate the text later can hold onto the returned control. Vertical
    /// alignment isn't set here — <see cref="AddRow"/> applies the row's
    /// chosen alignment to it, same as every other cell.
    /// </summary>
    public static TextBlock DescriptionText(string text) => new()
    {
        Text = text,
        FontStyle = FontStyle.Italic,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>
    /// A labeled, editable text field. Fires <paramref name="onChanged"/> with
    /// the box's current text on every keystroke — this only builds and wires
    /// the control; the caller decides whether a change is real (typically by
    /// comparing against the field's current live value, to avoid marking
    /// dirty on a no-op).
    /// <para>
    /// <c>VerticalContentAlignment.Center</c>: an earlier version tried
    /// pulling the box's text up toward a sibling label's <c>Top</c> instead
    /// (plus a trimmed <c>Padding</c>), but plain centering reads better
    /// in practice. Either way
    /// the box's border/frame still occupies more vertical space than plain
    /// text; that residual is the accepted cost of it being an editable
    /// control at all, not something alignment alone fixes.
    /// </para>
    /// </summary>
    public void AddEditableTextRow(string label, string value, Action<string> onChanged, double boxWidth = 240)
    {
        var box = new TextBox
        {
            Text = value,
            Width = boxWidth,
            VerticalContentAlignment = VerticalAlignment.Center,
            // Unlike ComboBox (see AddCustomizationRow's comment), the
            // Fluent theme leaves TextBox at the base Stretch default. When
            // a wider sibling in the same RowGroup column (e.g. General's
            // skin/hair color swatch readout) pushes the column past
            // boxWidth, a Stretch-aligned control with an explicit Width
            // centers in the extra space instead of sitting flush left —
            // the fixed-Width-plus-Stretch case degenerates to Center, not
            // Left, in Avalonia's layout system the same way it does in
            // WPF's. That misaligned the Player name box against every
            // other row's data column (found live, screenshot pixel-diffed
            // against the Sex/Beard/Hair combo boxes: a real 6px offset).
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.TextChanged += (_, _) => onChanged(box.Text ?? "");
        AddRow(label, box);
    }

    /// <summary>
    /// A labeled flag with a one-directional "clear" button instead of a
    /// checkbox — for fields where toggling back on has no legitimate use
    /// (e.g. the used-cheats flag) and offering a checkbox would silently
    /// turn a deliberate action into an ordinary bool edit. The button is
    /// enabled only while <paramref name="value"/> is true; clicking it calls
    /// <paramref name="onClear"/>, then updates its own label text and
    /// disables itself — the row owns its own post-click display state
    /// rather than depending on a tab rebuild. <paramref name="tooltip"/> is
    /// the button's own "what will this do" (mechanical); <paramref name="description"/>,
    /// if given, is separate context worth knowing that isn't obvious from
    /// the row itself — the two don't substitute for each other.
    /// </summary>
    public void AddClearableFlagRow(string label, bool value, string tooltip, Action onClear, string? description = null)
    {
        var valueText = TextCell(TabRows.FormatBool(value));

        var button = IconButtons.Create(IconButtons.ClearGlyph, tooltip);
        button.IsEnabled = value;
        button.Click += (_, _) =>
        {
            onClear();
            valueText.Text = TabRows.FormatBool(false);
            button.IsEnabled = false;
        };

        AddRow(label, valueText, button, description is null ? null : DescriptionText(description));
    }

    public Control Build() => _grid;

    private static TextBlock TextCell(string text) => new() { Text = text };

    private void Place(Control control, int column)
    {
        Grid.SetRow(control, _row);
        Grid.SetColumn(control, column);
        _grid.Children.Add(control);
    }
}
