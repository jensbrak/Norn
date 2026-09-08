using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>Known recipes/stations/material/uniques/trophies/biomes/texts,
/// plus shown tutorials. Read-only this pass (delete/clear actions
/// deliberately deferred). One shared
/// <see cref="RowGroup"/> as before, but reordered short-to-long:
/// biomes and stations (few items) first, uniques (a dozen or two) next,
/// then the five list-heavy sections (recipes, material, trophies, texts,
/// tutorials) — "shown tutorials" stays last within that tier regardless of
/// its position on the wire, per the original call (weakest
/// semantic fit, least interesting). Every section header shows its item
/// count via <see cref="RowGroup.AddSectionHeader"/>'s <c>count</c> param,
/// with a plain, unlabeled <see cref="RowGroup.AddDivider"/> before each
/// section but the first (2026-08-26 — see that method's doc comment: a
/// titled divider used to stand in for the header itself, replaced across
/// this tab, Statistics, and Worlds in the same pass). Every section's own
/// rows pass a <c>null</c> label to <see cref="RowGroup.AddRow"/>, which
/// reads as a slight, deliberate indent under its bold header — an
/// incidental side effect of the column layout that turned out to look
/// right, kept as-is rather than promoted to an explicit "indent" flag
/// RowGroup would need to formalize for one tab's benefit.
/// The five long sections no longer dump every raw string inline — that
/// wall of prefab/localization-key text was the concrete complaint driving
/// this pass — each collapses into an <see cref="Expander"/> instead, kept
/// closed by default; biomes/stations/uniques are short enough to stay
/// listed inline as before. Known stations is the one section that's
/// genuinely two-column data (name + level), not a single value — it used
/// <see cref="RowGroup.AddRow"/>'s own bold label column at first, which
/// made it read as a series of form fields against every other section's
/// plain indented list; it now renders via <see cref="TabRows.BuildPairedList"/>
/// instead, general-purpose enough for any future (primary, secondary)
/// list rather than a one-off fix. "Known texts" shows each entry's label
/// in the list, with the resolved body text as
/// a wrapping hover tooltip rather than inline — the body can run to
/// several paragraphs, which would defeat the point of collapsing this
/// section in the first place if shown inline by default.</summary>
public sealed class UnlockablesTabModule : ITabModule
{
    public string Title => "Unlockables";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var data = editor.View.Unlockables;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };
        var group = new RowGroup(rowSpacing: 4);

        AddSection(group, "Known biomes", data.KnownBiomes, collapse: false, isFirst: true);
        AddStationSection(group, "Known stations", data.KnownStations);
        AddSection(group, "Uniques", data.Uniques, collapse: false);
        AddSection(group, "Known recipes", data.KnownRecipes, collapse: true);
        AddSection(group, "Known material", data.KnownMaterial, collapse: true);
        AddSection(group, "Trophies", data.Trophies, collapse: true);
        AddKnownTextsSection(group, "Known texts", data.KnownTexts);
        AddSection(group, "Shown tutorials", data.ShownTutorials, collapse: true);

        panel.Children.Add(group.Build());
        return new ScrollViewer { Content = panel };
    }

    private static void AddSection(RowGroup group, string title, IReadOnlyList<string> values, bool collapse, bool isFirst = false)
    {
        if (!isFirst)
        {
            group.AddDivider();
        }

        group.AddSectionHeader(title, values.Count);

        if (values.Count == 0)
        {
            group.AddRow(null, new TextBlock { Text = "None recorded." });
            return;
        }

        if (collapse)
        {
            group.AddRow(null, TabRows.BuildExpander(values));
            return;
        }

        foreach (var value in values)
        {
            group.AddRow(null, new TextBlock { Text = value });
        }
    }

    private static void AddStationSection(RowGroup group, string title, IReadOnlyList<KnownStationDto> stations)
    {
        group.AddDivider();
        group.AddSectionHeader(title, stations.Count);

        if (stations.Count == 0)
        {
            group.AddRow(null, new TextBlock { Text = "None recorded." });
            return;
        }

        var pairs = stations.Select(station => (station.Name, $"Level {station.Level}"));
        group.AddRow(null, TabRows.BuildPairedList(pairs));
    }

    /// <summary>Known texts, same closed-by-default <see cref="Expander"/>
    /// shape as <see cref="TabRows.BuildExpander"/>, but each row also carries the
    /// resolved body text as a hover tooltip — the one field where a
    /// second, longer piece of text exists per entry, so it can't reuse
    /// the plain-string list builder above. The tooltip is built as its
    /// own wrapping <see cref="TextBlock"/> rather than a plain string:
    /// Avalonia's default tooltip template doesn't reliably wrap long
    /// content, and a body running to several sentences needs it to.</summary>
    private static void AddKnownTextsSection(RowGroup group, string title, IReadOnlyList<KnownTextDto> texts)
    {
        group.AddDivider();
        group.AddSectionHeader(title, texts.Count);

        if (texts.Count == 0)
        {
            group.AddRow(null, new TextBlock { Text = "None recorded." });
            return;
        }

        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        foreach (var text in texts)
        {
            var row = new TextBlock { Text = text.Label };
            ToolTip.SetTip(row, new TextBlock
            {
                Text = text.Text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
            });
            list.Children.Add(row);
        }

        group.AddRow(null, new Expander
        {
            Header = "Show items",
            Content = list,
            IsExpanded = false,
        });
    }
}
