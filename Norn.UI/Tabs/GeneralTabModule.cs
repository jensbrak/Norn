using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>Identity, save version, and file-independent metadata. Player
/// name is freely editable; used-cheats
/// is one-directional (clear only, no re-enable — see
/// <see cref="RowGroup.AddClearableFlagRow"/>); skin/hair color are editable
/// via sliders matching the game's own mechanism, anchored to the
/// character's actual current color rather than an arbitrary starting point
/// beard/hair style are editable via a dropdown
/// constrained to <see cref="CustomizationCatalog"/>'s entries — never a free
/// prefab-name string, which is what keeps this an ordinary editable field
/// rather than the unsupported-feature-gating territory a raw text box would
/// fall into; model index is editable via a two-entry Male/Female dropdown
/// (<see cref="CharacterModels"/>), deliberately independent of beard/hair;
/// everything else here
/// stays read-only.
/// <para>
/// Two sections — identity/meta (through "First spawn") and appearance
/// (model index, then beard/hair/color from there) — divided by an
/// unlabeled <see cref="RowGroup.AddDivider"/>:
/// the former is fixed at character creation or one-time state, the latter
/// is chosen at creation and normally stays static for the save's life, but
/// the two read as different *kinds* of fact even so.
/// Both sections share one <see cref="RowGroup"/> (and so the same column
/// widths/tab-stops) rather than one each — independently
/// scoped columns per section read as "weird and chaotic" in practice, so
/// the whole tab aligns as a single table with a rule across the middle.
/// </para></summary>
public sealed class GeneralTabModule : ITabModule
{
    public string Title => "General";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var meta = editor.View.Meta;
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };

        var group = new RowGroup();
        group.AddEditableTextRow("Player name", meta.PlayerName, text =>
        {
            if (text != editor.View.Meta.PlayerName)
            {
                editor.SetPlayerName(text);
                onEdited();
            }
        });
        group.AddRow("Player ID", meta.PlayerId.ToString());
        group.AddRow("Start seed", meta.StartSeed);
        group.AddRow("Profile version", meta.ProfileVersion.ToString());
        group.AddRow("Player-data version", meta.PlayerDataVersion.ToString());
        group.AddRow("Date created", meta.DateCreated.ToString("yyyy-MM-dd"));
        group.AddClearableFlagRow("Used cheats", meta.UsedCheats, "Clears the cheat flag.", () =>
        {
            editor.ClearUsedCheats();
            onMessage("Cheat flag and cheats counter cleared");
            onEdited();
        },
        // The mechanical fact ("clears the flag") now lives on the button's
        // own tooltip; this slot is freed up for the one thing about this
        // row that isn't obvious from anywhere else — the paired Statistics
        // counter — stated up front rather than only after the
        // fact in the click's own status-bar message.
        description: "Also updates the Cheats count shown on the Statistics tab.");
        group.AddRow("First spawn", meta.FirstSpawn);

        // WorldsDto.KnownCommands, not MetaDto — the wire format writes it
        // back-to-back with two genuinely per-world dictionaries, but it
        // isn't a per-world fact itself; it's the detailed backing list for
        // "Used cheats" above (same character-history/meta category), so it
        // renders here instead. Placed below "First spawn," not directly
        // under "Used cheats" — an eyeballed call once both were on screen
        // together.
        // Per-command invocation count now shown: confirmed a clean cumulative
        // count with no other unit mixed in, unlike KnownWorldKeys' float —
        // safe to display, unlike the caution that held this back initially.
        // Sorted by count descending: "which commands you actually use" is
        // the more interesting reading of this data than insertion order.
        var knownCommands = editor.View.Worlds.KnownCommands;
        if (knownCommands.Count > 0)
        {
            // Width matched to this tab's ComboBoxes (240) — General is
            // otherwise a column of fixed-width editable
            // controls, and TabRows.BuildExpander's own default (content-sized,
            // matching Unlockables' usage, which has no such column to match)
            // would read as visually inconsistent here specifically.
            var commandLines = knownCommands
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key} ({(int)kv.Value})")
                .ToList();
            var commandsExpander = TabRows.BuildExpander(commandLines);
            commandsExpander.Width = 240;

            // alignTop: once expanded, this row's height comes from the
            // Expander's own top-to-bottom list content (RowGroup.AddRow's
            // own documented Top-vs-Center rule) — without it the label
            // center-tracks the expanded height and visibly drifts downward
            // relative to the row above it.
            group.AddRow($"Known commands ({knownCommands.Count})", commandsExpander, alignTop: true);
        }

        // Player.m_customData — a mod-extension point, confirmed via
        // the decompiled source to be written by no vanilla game system in
        // either version. Shown only when non-empty:
        // empty is the overwhelming common case, and an always-visible
        // "Custom data: none" row would just be noise on every unmodded
        // save.
        if (meta.CustomData.Count > 0)
        {
            group.AddRow($"Custom data (mod) ({meta.CustomData.Count})",
                TabRows.BuildPairedList(meta.CustomData.Select(kv => (kv.Key, kv.Value))));
        }

        group.AddDivider();

        // Placed topmost of appearance data since it's the most structural
        // of the fields here.
        AddModelRow(group, meta.ModelIndex, index => { editor.SetModelIndex(index); onEdited(); });
        AddCustomizationRow(group, "Beard", meta.BeardItem, CustomizationCatalog.Beards(),
            CustomizationCatalog.Trophies(),
            name => { editor.SetBeardItem(name); onEdited(); });
        AddCustomizationRow(group, "Hair", meta.HairItem, CustomizationCatalog.Hairs(),
            CustomizationCatalog.Trophies(),
            name => { editor.SetHairItem(name); onEdited(); });
        group.AddRow("Skin color", BuildSkinColorData(meta, editor, onEdited), alignTop: true);
        group.AddRow("Hair color", BuildHairColorData(meta, editor, onEdited), alignTop: true);

        panel.Children.Add(group.Build());

        return new ScrollViewer { Content = panel };
    }

    /// <summary>
    /// A beard/hair style dropdown, built from <see cref="CustomizationCatalog"/>'s
    /// filtered, sorted entries — the same seam Inventory's <c>SharedItemDataCatalog</c>
    /// resolution uses (<c>shared?.DisplayName ?? item.PrefabName</c>-style
    /// fallback), applied here to the picker case instead of a display label.
    /// <paramref name="trophyOptions"/> (<see cref="CustomizationCatalog.Trophies"/>)
    /// append after a disabled separator entry, last in the list — a Norn-only
    /// bonus, not a game-supported choice, so it never sits ahead of or mixed
    /// into the real styles (a future opt-in-unlock gate is the natural
    /// place to make this section conditional).
    /// <para>
    /// Five edge cases, all resolved the same way — never silently changing
    /// the save on tab open: an empty <paramref name="options"/> list (no
    /// catalog resolved at all) falls back to the pre-existing plain
    /// read-only row, regardless of whether trophies resolved; a
    /// <paramref name="currentValue"/> that already is a trophy (set by
    /// Norn's own trophy option, or by another tool) resolves straight to
    /// its entry in the trophy group instead of being treated as
    /// unrecognized; an empty <paramref name="currentValue"/> — the game's
    /// own no-style-chosen representation, not unrecognized data — resolves
    /// straight to whichever <c>&lt;prefix&gt;None</c> catalog entry
    /// <see cref="Filter"/> already sorts to the front of the offered list
    /// (e.g. <c>HairNone</c>, "No Hair"), the same way a trophy currentValue
    /// does, rather than falling into the next case; a
    /// <paramref name="currentValue"/> that isn't among either offered list
    /// but does resolve against the full, unfiltered catalog
    /// (<see cref="SharedItemDataCatalog.TryFind"/>) — a real style whose
    /// underscore-suffixed variant prefab <see cref="Filter"/> deliberately
    /// excludes from the picker, e.g. <c>Hair4_3</c> ("Pigtails"), which the
    /// game itself can still equip even though its own creation screen
    /// never offers it as a separate choice — shows its real display name
    /// in the synthetic leading entry rather than the raw prefab name; only
    /// a <paramref name="currentValue"/> the catalog has genuinely never
    /// heard of (a legacy save, a value written outside Norn) falls back to
    /// showing the raw name, in both of the last two cases as a synthetic
    /// leading entry rather than forced onto whatever catalog entry happens
    /// to sort first.
    /// </para>
    /// </summary>
    private static void AddCustomizationRow(
        RowGroup group,
        string label,
        string currentValue,
        IReadOnlyList<SharedItemDataDto> options,
        IReadOnlyList<SharedItemDataDto> trophyOptions,
        Action<string> onSelected)
    {
        if (options.Count == 0)
        {
            group.AddRow(label, currentValue);
            return;
        }

        var names = options.Select(o => o.ItemName).ToList();
        var labels = options.Select(o => o.DisplayName).ToList();

        var separatorIndex = -1;
        if (trophyOptions.Count > 0)
        {
            separatorIndex = names.Count;
            // null, not string.Empty: a real currentValue can legitimately be
            // "" (no beard equipped, or the game's auto-clear-beard-on-female
            // convenience), and names.IndexOf(currentValue) below must never
            // match the separator's own placeholder slot.
            names.Add(null!);
            labels.Add(string.Empty);
            names.AddRange(trophyOptions.Select(o => o.ItemName));
            labels.AddRange(trophyOptions.Select(o => o.DisplayName));
        }

        var currentIndex = names.IndexOf(currentValue);
        if (currentIndex < 0 && currentValue.Length == 0)
        {
            // "" isn't unrecognized data - it's the game's own explicit
            // no-style-chosen representation (a freshly created character,
            // or the auto-clear-beard-on-female convenience), and the
            // catalog already has a real entry for exactly that state:
            // Filter() sorts whichever <prefix>None row (e.g. HairNone,
            // "No Hair") exists to the front of options whenever there's
            // more than one entry. Resolve straight to it instead of
            // falling into the TryFind/raw-name path below, which only
            // ever misses for "" (nothing is ever keyed by an empty name)
            // and would otherwise mislabel a normal, common state as
            // "(not a recognized style)".
            var none = options.FirstOrDefault(o => o.ItemName.EndsWith("None", StringComparison.Ordinal));
            if (none is not null)
            {
                currentIndex = names.IndexOf(none.ItemName);
            }
        }

        if (currentIndex < 0)
        {
            // Not among the offered choices doesn't mean unrecognized data:
            // Filter() deliberately excludes underscore-suffixed variant
            // prefabs (e.g. Hair4_3) from the picker because the game's own
            // creation screen never offers them as a distinct choice either
            // — but the game *can* still equip one on a character (observed
            // on a live save), and it resolves to a real catalog entry
            // (Hair4_3 -> "Pigtails"). Only fall back to the raw-name,
            // truly-unrecognized label when even the unfiltered catalog
            // (SharedItemDataCatalog.TryFind, no variant-suffix filtering)
            // has never heard of it.
            var resolved = SharedItemDataCatalog.TryFind(currentValue);
            var fallbackLabel = resolved is null
                ? $"{currentValue} (not a recognized style)"
                : $"{resolved.DisplayName} (variant not offered here)";

            names.Insert(0, currentValue);
            labels.Insert(0, fallbackLabel);
            currentIndex = 0;
            if (separatorIndex >= 0)
            {
                separatorIndex++;
            }
        }

        // No explicit HorizontalAlignment: ComboBox's Fluent theme already
        // defaults to Left (SaveFileListControls' own comment documents
        // this quirk), same as every other fixed-width editable control in
        // this row group.
        var combo = new ComboBox { Width = 240 };
        for (var i = 0; i < labels.Count; i++)
        {
            combo.Items.Add(i == separatorIndex
                ? new ComboBoxItem { Content = "Trophies (experimental)", IsEnabled = false, FontStyle = FontStyle.Italic }
                : labels[i]);
        }

        combo.SelectedIndex = currentIndex;
        combo.SelectionChanged += (_, _) =>
        {
            // The separator can't be reached by pointer or keyboard (a
            // disabled item), but guard the index anyway rather than trust
            // that behavior across every input path.
            if (combo.SelectedIndex == separatorIndex)
            {
                return;
            }

            onSelected(names[combo.SelectedIndex]);
        };

        group.AddRow(label, combo);
    }

    /// <summary>
    /// Same synthetic-entry-on-unrecognized-value safety as
    /// <see cref="AddCustomizationRow"/>, sourced from
    /// <see cref="CharacterModels.Known"/> — a fixed two-entry
    /// <c>Norn.Adapter</c> lookup rather than a CSV catalog, so there's no
    /// empty-catalog fallback case: the two known entries always exist.
    /// Deliberately independent of the beard/hair rows above — see
    /// <see cref="CharacterEditor.SetModelIndex"/>'s doc comment for why
    /// Norn doesn't replicate the game's own auto-clear-beard-on-female
    /// convenience here.
    /// </summary>
    private static void AddModelRow(RowGroup group, int currentValue, Action<int> onSelected)
    {
        var indexes = CharacterModels.Known.Select(m => m.Index).ToList();
        var labels = CharacterModels.Known.Select(m => m.Label).ToList();

        var currentIndex = indexes.IndexOf(currentValue);
        if (currentIndex < 0)
        {
            indexes.Insert(0, currentValue);
            labels.Insert(0, $"Unrecognized model index ({currentValue})");
            currentIndex = 0;
        }

        var combo = new ComboBox { Width = 240 };
        foreach (var text in labels)
        {
            combo.Items.Add(text);
        }

        combo.SelectedIndex = currentIndex;
        combo.SelectionChanged += (_, _) => onSelected(indexes[combo.SelectedIndex]);

        group.AddRow("Sex", combo);
    }

    /// <summary>
    /// One slider (matching <c>PlayerCustomizaton</c>'s single skin-hue
    /// slider — see <see cref="AppearanceColors.SkinColorAt"/>) plus a live
    /// swatch/readout beneath it — the group's own label ("Skin color:")
    /// covers both sub-rows, so only this cell's internal content is
    /// multi-row, not the surrounding group. Seeded via
    /// <see cref="AppearanceColors.SolveSkinHue"/>, reverse-solved from the
    /// character's actual stored color rather than a fixed anchor — an
    /// earlier version that started at a fixed position was found unusable
    /// in practice ("just a slider without feel of anchoring").
    /// </summary>
    private static Control BuildSkinColorData(MetaDto meta, CharacterEditor editor, Action onEdited)
    {
        var outer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        var initialHue = AppearanceColors.SolveSkinHue(meta.SkinColor);
        var initialColor = AppearanceColors.SkinColorAt(initialHue);

        var swatch = BuildSwatch(initialColor);
        var readout = new TextBlock { Text = AppearanceColors.FormatColor(initialColor), VerticalAlignment = VerticalAlignment.Top };

        var slider = new Slider { Minimum = 0, Maximum = 1, Value = initialHue, Width = 200, VerticalAlignment = VerticalAlignment.Top };
        slider.ValueChanged += (_, _) =>
        {
            var color = AppearanceColors.SkinColorAt((float)slider.Value);
            editor.SetSkinColor(color);
            swatch.Background = ToBrush(color);
            readout.Text = AppearanceColors.FormatColor(color);
            onEdited();
        };

        outer.Children.Add(BuildSubRow("Hue:", slider));
        outer.Children.Add(BuildSwatchRow(swatch, readout));

        return outer;
    }

    /// <summary>
    /// Tone and level each get their own sub-row rather than sharing one —
    /// two identically-styled Avalonia sliders sharing a row read as one
    /// confusing control with an odd middle section, not two. Seeded via
    /// <see cref="AppearanceColors.SolveHairToneLevel"/>, same reasoning as
    /// <see cref="BuildSkinColorData"/>.
    /// </summary>
    private static Control BuildHairColorData(MetaDto meta, CharacterEditor editor, Action onEdited)
    {
        var outer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        var (initialTone, initialLevel) = AppearanceColors.SolveHairToneLevel(meta.HairColor);
        var initialColor = AppearanceColors.HairColorAt(initialTone, initialLevel);

        var swatch = BuildSwatch(initialColor);
        var readout = new TextBlock { Text = AppearanceColors.FormatColor(initialColor), VerticalAlignment = VerticalAlignment.Top };

        var toneSlider = new Slider { Minimum = 0, Maximum = 1, Value = initialTone, Width = 200, VerticalAlignment = VerticalAlignment.Top };
        var levelSlider = new Slider { Minimum = 0, Maximum = 1, Value = initialLevel, Width = 200, VerticalAlignment = VerticalAlignment.Top };

        void Recompute()
        {
            var color = AppearanceColors.HairColorAt((float)toneSlider.Value, (float)levelSlider.Value);
            editor.SetHairColor(color);
            swatch.Background = ToBrush(color);
            readout.Text = AppearanceColors.FormatColor(color);
            onEdited();
        }

        toneSlider.ValueChanged += (_, _) => Recompute();
        levelSlider.ValueChanged += (_, _) => Recompute();

        outer.Children.Add(BuildSubRow("Tone:", toneSlider));
        outer.Children.Add(BuildSubRow("Level:", levelSlider));
        outer.Children.Add(BuildSwatchRow(swatch, readout));

        return outer;
    }

    private const double SubLabelWidth = 44;

    /// <summary>
    /// A small labeled control within a multi-row data cell — e.g. the
    /// "Hue:" slider inside "Skin color"'s data. Both the sub-label and its
    /// slider are Top-aligned: this sub-row is the first line of a
    /// multi-part data block (see the "Skin color"/"Hair color" row's
    /// <c>alignTop: true</c>), so it needs to sit flush with the outer
    /// "Skin color:"/"Hair color:" label rather than centered against its
    /// own height — a residual small gap between the label's text and the
    /// slider's own visual thumb (the slider control has more internal
    /// padding than a line of text) is the accepted "not fighting Avalonia's
    /// control templates" cost.
    /// </summary>
    private static Control BuildSubRow(string label, Control control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Width = SubLabelWidth, VerticalAlignment = VerticalAlignment.Top });
        row.Children.Add(control);
        return row;
    }

    private static Control BuildSwatchRow(Border swatch, TextBlock readout)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(swatch);
        row.Children.Add(readout);
        return row;
    }

    private static Border BuildSwatch(ColorDto color) => new()
    {
        Width = 20,
        Height = 20,
        Background = ToBrush(color),
        BorderBrush = Brushes.Gray,
        BorderThickness = new Avalonia.Thickness(1),
    };

    private static IBrush ToBrush(ColorDto color) => new SolidColorBrush(Color.FromRgb(
        (byte)Math.Clamp(color.R * 255, 0, 255),
        (byte)Math.Clamp(color.G * 255, 0, 255),
        (byte)Math.Clamp(color.B * 255, 0, 255)));
}
