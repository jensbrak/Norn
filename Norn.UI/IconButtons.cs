using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

/// <summary>
/// Norn's fixed-square icon-button shape, and its one "clear" glyph — a
/// single source for both so every clear action in the app looks the same
/// by construction, not by each call site happening to copy the last one.
/// Originally derived for the save-list's clear-search/sort-direction
/// buttons (<see cref="SaveFileListControls"/>); two Fluent-theme quirks
/// found there apply to any icon button, not just those two: Fluent's
/// default Button padding (8,5,8,6) is sized for text labels and clips a
/// glyph on a small square button, and the Fluent theme template-binds
/// content alignment straight through from the instance rather than
/// defaulting either axis to Center the way most controls do (matches
/// AvaloniaUI/Avalonia#11786).
/// </summary>
internal static class IconButtons
{
    public const string ClearGlyph = "✕";

    /// <summary>"Restore to max" — deliberately distinct from <see cref="ClearGlyph"/>:
    /// this is a maximizing action, not a clearing one (Vitals tab), and sharing one
    /// glyph for both would blur that distinction visually.</summary>
    public const string MaximizeGlyph = "↑";

    /// <summary>"View map" (Worlds tab), settled 2026-08-24 after several
    /// rejected rounds — the verdict was "not perfect but the best
    /// match so far." U+00BB, Latin-1 Supplement — ordinary typographic
    /// punctuation, about as universally supported as a character gets.
    /// <para>
    /// The deciding reasoning, once literal "map/globe/eye" pictograms kept
    /// failing: <see cref="ClearGlyph"/>/<see cref="MaximizeGlyph"/> aren't
    /// pictures of what they do either (✕ isn't a trash can, ↑ isn't a
    /// "biggest" icon) — they're a meta-language, a symbolic convention for
    /// "this kind of action," and » matches that same abstraction level
    /// ("see more / expand") rather than trying to visually represent a map.
    /// </para>
    /// <para>
    /// Rejected candidates and why, in order: ▦ (U+25A6, Geometric Shapes)
    /// — an obscure corner of an otherwise-safe block. ⊙ (U+2299,
    /// Mathematical Operators) — safe block, but not recognizable as "map."
    /// 🌐/👁 (globe/eye emoji) — colored, breaks the monochrome look every
    /// other icon button here shares. ⬤ (U+2B24) — right color, but visibly
    /// off-center/off-weight against ✕/↑. ⇲/↗/↕ (Arrows block, same block
    /// as ↑) — weight-matched by construction, but "expand to corner" reads
    /// as nothing in particular. ℹ (U+2139) — near-universal "info" glyph,
    /// but renders in color (same problem as the emoji). ⌖ (U+2316,
    /// Miscellaneous Technical) — a literal "location on a map" meaning,
    /// but too small relative to ✕/↑.
    /// </para></summary>
    // note: "»»" (ExploreAllGlyph) lived here 2026-09-01 through the "Reveal
    // world" move into WorldMapWindow — a plain text Button there now,
    // alongside the window's other two action buttons, not an icon glyph.
    public const string ViewMapGlyph = "»";

    public const double Size = 28;

    /// <summary>Standard gap between a text box and a trailing icon button
    /// docked to its right in a <see cref="DockPanel"/> (e.g. a search box's
    /// clear button) — without it the two visually collide, since neither
    /// control has its own edge padding here. Originally a private constant
    /// on <see cref="SaveFileListControls"/> alone; promoted once
    /// <see cref="AddItemWindow"/> needed the identical spacing for its own
    /// search row and a missing-margin bug showed the
    /// value wasn't actually shared yet.</summary>
    public static readonly Thickness ButtonSpacing = new(6, 0, 0, 0);

    private static readonly Thickness Padding = new(0);

    /// <summary>
    /// <paramref name="tooltip"/> is required, not optional: every icon
    /// button in the app gets one, uniformly — no "is this one dangerous
    /// enough" or "is it already explained elsewhere" judgment call (an
    /// earlier version of this method took a <c>destructive: bool</c> flag
    /// and only some buttons got a tooltip; settled instead on every button
    /// explaining what it does, always, with genuinely extra context living
    /// separately in a row's description or a tab's legend, judged only on
    /// its own merit). <see cref="Settings.ShowButtonTooltips"/> is checked
    /// here rather than per call site — every icon button in the app goes
    /// through this one factory, so this is the single place that needs to
    /// know the setting exists at all. Read once, at build time, same as
    /// every other Settings-gated behavior in the app — a toggle flipped
    /// mid-session takes effect the next time this tab is rebuilt, not live
    /// on an already-open one.
    /// </summary>
    public static Button Create(string glyph, string tooltip, double size = Size)
    {
        var button = new Button
        {
            Content = glyph,
            Width = size,
            Height = size,
            Padding = Padding,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        if (SettingsStore.Current.ShowButtonTooltips)
        {
            ToolTip.SetTip(button, tooltip);
        }

        return button;
    }
}
