using Avalonia.Controls;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>Per-world spawn/logout/death/home coordinates, a "View map"
/// button, and two structural edits: "Clear map data" (nulls the world's
/// map blob — exploration and every placed pin go together, since both live
/// inside the same opaque byte[]; see <see cref="CharacterEditor.ClearWorldMapData"/>)
/// and "Remove world" (drops the whole per-character entry, keyed by world
/// UID — <see cref="CharacterEditor.RemoveWorld"/>). Neither prompts for
/// confirmation, matching Vitals' per-row food removal — Norn's existing
/// convention for row-level deletes.
/// <para>
/// "Remove world" sits on the world's own section heading
/// (<see cref="RowGroup.AddSectionHeader"/>'s <c>button</c> param, built for
/// this — see that method's doc comment for the full "a divider isn't a
/// header" story) — settled after first trying it as its own
/// labeled row: "it's a title of the world... 'remove' is almost explicit"
/// once it's right next to the name of the thing it removes, so no
/// separate label/description is needed there. "Clear map data" stays a
/// per-row action instead (next to "Has map data", alongside "View map") —
/// how it differs from removing the world outright (the two actions look
/// similar, same ✕, both destructive, but aren't nested or ordered relative
/// to each other) is stated once in <see cref="ClearActionsLegendExplanation"/>
/// above the whole list rather than repeated on every world's own row; see
/// that field's own doc comment.
/// </para>
/// <para>
/// "Explore All" (<see cref="CharacterEditor.ExploreAllMap"/>) sits in the
/// same header button slot as "Remove world",
/// to its left — the first time this tab's shared button column has held two
/// controls on one row rather than one, which is what makes it line up above
/// the "Has map data" row's »/✕ pair below rather than needing its own
/// alignment logic (<see cref="RowGroup.AddSectionHeader"/>'s doc comment
/// already covers why a lone button right-aligns against that shared
/// column). Unlike "Remove world"/"Clear map data," this isn't a removal —
/// it's the one write path in the whole app that replaces a compressed map
/// blob wholesale rather than nulling or patching a decoded value (see
/// <see cref="CharacterEditor.ExploreAllMap"/>'s own doc comment) — so
/// its caution gets a separate dismissible legend
/// (<see cref="ExploreAllLegendExplanation"/>/<see cref="Settings.ShowExploreWorldWarning"/>)
/// rather than joining <see cref="ClearActionsLegendExplanation"/>'s.
/// </para>
/// <para>
/// Self-rebuilding, same shape as <c>InventoryTabModule</c>: <see cref="Build"/>
/// wires a container <see cref="StackPanel"/> once, and every mutating button
/// calls <c>Rebuild</c> again on it after the edit, since a world
/// disappearing (or a "Has map data"/button-enabled state flipping) needs to
/// be visible immediately, not just on the next file reload — unlike
/// <c>onEdited</c>, which is deliberately chrome-only and never
/// rebuilds tab content on its own.
/// </para>
/// <para>
/// One shared <see cref="RowGroup"/> across every world, with a plain
/// <see cref="RowGroup.AddDivider"/> before every world but the first
/// (Statistics/Unlockables' shape too, as of the same pass — "several named
/// sections that should share tab-stops," each with a real separator before
/// it rather than the separator standing in for the section's own heading)
/// — reversing an earlier, deliberate call to use one <see cref="RowGroup"/>
/// per world instead. That call predates the "View map" button below: with
/// only plain text in the data column, each world's own column width didn't
/// matter enough to notice. A button in that column made the drift obvious —
/// it landed at a different horizontal position per world, following
/// whichever world had the longest coordinate string, not a shared alignment
/// like every other icon button in the app has. This tab's layout is still
/// expected to change further once it gets its own map-like view
/// this fixes the immediate readability bug without
/// pretending to be that redesign.
/// </para>
/// <para>
/// A world's section header grows a parenthetical resolved name — e.g.
/// "World 3725283970 (Hearthhold)" — when <see cref="WorldDto.Identity"/> is
/// non-null, with a "Seed" row just below. Two further rows are conditional:
/// "Also seen as" lists every other local filename stem this UID has ever
/// been scanned under (e.g. a world copied outside the game under a new
/// name) — shown whenever more than one is known, alphabetically, plain
/// bookkeeping rather than a warning; "Note" is rarer and does carry a
/// warning — a first-seen name/seed conflict recorded for that UID. Purely
/// local enrichment, never derived from
/// the character save itself — absent whenever the UID has never matched a
/// local <c>.fwl</c>.
/// </para>
/// <para>
/// Spawn/logout/home each carry a ✕ (<see cref="AddPointRow"/>) that clears
/// them; death point gets no ✕ at all — source-reading confirmed nothing in
/// the game ever reads that field, so a control that "clears" it would
/// imply an effect it can't back up. What clearing each point (and clearing
/// map data, and how that differs from removing a world) actually does
/// in-game is stated once, in
/// <see cref="ClearActionsLegendExplanation"/> above the whole list, not
/// per-row — an earlier version repeated a full sentence per point *and*
/// a separate one for map data, both on every world, which read as the
/// description column dominating the tab rather than explaining it
/// (fighting the idea of "adding a description where needed"). One
/// paragraph covering both, not two blocks, once map data's
/// own description joined the consolidation — different facts, same *kind*
/// of fact ("what does this destructive ✕ do"), so keeping them together
/// reads as one reference rather than a wall of near-identical blocks.
/// Built later (2026-08-29, same day as <see cref="Settings.ShowButtonTooltips"/>
/// itself): each ✕ also carries its own real <see cref="ToolTip"/>
/// (<see cref="AddPointRow"/>'s <c>tooltip</c> param) stating only the
/// mechanical fact ("clears the spawn point") — this doesn't reopen the
/// consolidation above, since a tooltip and the shared legend answer
/// different questions ("what does clicking this do" vs. "what should I be
/// careful about on this whole tab"), not the same one twice. The same four
/// points, plus a legend, are also drawn
/// on <see cref="WorldMapWindow"/>'s map view via a "Show points" toggle —
/// same "state it once, don't repeat it per item" reasoning as that
/// window's own marker legend.
/// </para>
/// <para>
/// A one-time explanation (<see cref="IdentityEnrichmentExplanation"/>) sits
/// once above the whole world list, not repeated per row — prompted by a
/// real gap: a user seeing name/seed on some worlds and
/// not others, with no visible reason why, would reasonably read the gap as
/// a bug rather than "bonus info Norn happened to find locally." Shown
/// unconditionally whenever the tab has any worlds at all, not only when
/// some are actually unresolved, since the explanation appearing/
/// disappearing based on data would itself look like something changed.
/// Wording chosen deliberately over more literal phrasing —
/// "local save files"/"your machine" read as too vague or too stiff, and
/// "hosted" alone risked not landing, but "games other players host" does.
/// </para></summary>
public sealed class WorldsTabModule : ITabModule
{
    public string Title => "Worlds";

    private const string IdentityEnrichmentExplanation =
        "Names and seeds shown below come from matching local world files on this computer. "
        + "Worlds played by joining hosted games by others will not show additional info.";

    /// <summary>
    /// A blunt one-liner, not a reference — landed here 2026-08-27 after two
    /// full rewrites of a paragraph-length version tried to actually teach
    /// the mechanics and kept feeling
    /// wrong at every length. The real problem wasn't wording: how much
    /// explanation a fact like "clearing this point" needs isn't fixed, it
    /// depends on what the reader already knows — the worked
    /// example (a real, game-supported "bed destroyed while exploring far
    /// from a portal → die → wake at world origin, gear lost" disaster,
    /// reproducible in Norn by clearing Spawn point) landed very differently
    /// once you already know that scenario exists versus not — no single
    /// paragraph serves both readers. A teaching paragraph that fits an
    /// inexperienced reader is too much for an experienced one and still
    /// might not be enough for a genuinely new player; a warning is the one
    /// thing that's right-sized for everyone, on the theory that a detailed
    /// **Help** surface (parked, alongside the not-yet-built Settings
    /// surface) is the actual right home for the
    /// mechanics themselves, not this tab's shared legend.
    /// <para>
    /// Doesn't link anywhere (no Help surface exists yet) — a deliberate,
    /// accepted gap for now, not an oversight: the warning stands on its own
    /// today and gains a destination later. "Death point has no known use in
    /// game" is factual, not just a caution — it's also this row's entire
    /// explanation for why it has no ✕ (<see cref="Rebuild"/>), stated
    /// plainly rather than spelled out as cause-and-effect. "Removal only
    /// affects the current character" deliberately covers all three ✕
    /// actions on this tab with one clause, including "Remove world" (which
    /// still never touches the actual Valheim world file, only this
    /// character's own record of it) — simpler than the previous version's
    /// separate carve-out for that case. Deliberately dropped versus the
    /// previous paragraph: the "Clear map data" vs. "Remove world"
    /// distinction (they look similar, aren't nested/ordered relative to
    /// each other) isn't restated here — judged as Help-surface material,
    /// not "could hurt you" material, unlike the points/map-data warning
    /// itself.
    /// </para>
    /// <para>
    /// The "not-yet-built Settings surface" above is no longer accurate —
    /// Settings exists now (2026-08-29), and this paragraph (plus
    /// <see cref="IdentityEnrichmentExplanation"/>) is dismissible through it
    /// (<see cref="Settings.ShowWorldsRemovalWarning"/>/
    /// <see cref="Settings.ShowWorldsIdentityInfo"/>), independent of each
    /// other and recoverable if dismissed by mistake.
    /// </para></summary>
    private const string ClearActionsLegendExplanation =
        "Removal only affects the current character, but please make sure you understand how "
        + "it affects your character. Death point has no known use in game.";

    /// <summary>
    /// A different kind of caution than <see cref="ClearActionsLegendExplanation"/> —
    /// not data loss, but a gameplay spoiler: "Explore All" replicates the
    /// game's own (cheat-only, host-only) <c>exploremap</c> console command,
    /// filling in the entire map as if walked in person. Kept as its own
    /// dismissible legend (<see cref="Settings.ShowExploreWorldWarning"/>),
    /// not folded into <see cref="ClearActionsLegendExplanation"/>'s
    /// paragraph — that one's unifying theme is "removal only affects this
    /// character," which isn't the concern here at all.
    /// </summary>
    private const string ExploreAllLegendExplanation =
        "\"Explore All\" reveals a world's entire map at once — the same effect as the game's own "
        + "(cheat-only) \"exploremap\" command. It only fills in your own exploration, not map data "
        + "shared with you via a cartography table, and only affects the current character.";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };

        if (SettingsStore.Current.ShowWorldsIdentityInfo)
        {
            panel.Children.Add(BuildDismissibleLegend(
                IdentityEnrichmentExplanation,
                () => SettingsStore.Current.ShowWorldsIdentityInfo = false));
        }

        if (SettingsStore.Current.ShowWorldsRemovalWarning)
        {
            panel.Children.Add(BuildDismissibleLegend(
                ClearActionsLegendExplanation,
                () => SettingsStore.Current.ShowWorldsRemovalWarning = false));
        }

        if (SettingsStore.Current.ShowExploreWorldWarning)
        {
            panel.Children.Add(BuildDismissibleLegend(
                ExploreAllLegendExplanation,
                () => SettingsStore.Current.ShowExploreWorldWarning = false));
        }

        var container = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(container);

        Rebuild(container, editor, onEdited, onMessage);

        return new ScrollViewer { Content = panel };
    }

    /// <summary>
    /// A legend paragraph plus its own dismiss ✕, docked right and
    /// top-aligned against the wrapped text (same top-align-for-multi-line-
    /// prose rule <see cref="RowGroup.AddRow"/> itself documents). Dismissing
    /// persists immediately (<see cref="SettingsStore"/>'s usual
    /// auto-save) and hides this row in place — no tab rebuild needed, same
    /// self-owned post-click state <see cref="RowGroup.AddClearableFlagRow"/>
    /// already uses. Both of this tab's legends get the same treatment
    /// regardless of which reads more like a warning and which reads more
    /// like plain explanation — that split is just how the current wording
    /// happens to land, not a structural difference worth two mechanisms.
    /// </summary>
    private static Control BuildDismissibleLegend(string text, Action onDismiss)
    {
        var row = new DockPanel { Margin = new Avalonia.Thickness(0, 0, 0, 12) };

        var dismiss = IconButtons.Create(IconButtons.ClearGlyph, "Dismiss this message.");
        dismiss.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(dismiss, Dock.Right);
        dismiss.Click += (_, _) =>
        {
            onDismiss();
            SettingsStore.Save();
            row.IsVisible = false;
        };
        row.Children.Add(dismiss);

        row.Children.Add(RowGroup.DescriptionText(text));

        return row;
    }

    private static void Rebuild(StackPanel container, CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        container.Children.Clear();

        var data = editor.View.Worlds;
        var worlds = data.Worlds;

        if (worlds.Count == 0)
        {
            // KnownWorlds is stored independently of the per-world entries
            // and survives removing every one of them, so this early return
            // used to hide real, still-present playtime history the moment
            // the last world was removed (found in review). Render it on its
            // own instead.
            if (data.KnownWorlds.Count > 0)
            {
                var playtimeOnly = new RowGroup();
                playtimeOnly.AddSectionHeader("Known worlds (playtime)", data.KnownWorlds.Count);
                playtimeOnly.AddRow(
                    null,
                    TabRows.BuildPairedList(data.KnownWorlds.Select(kv => (kv.Key, TabRows.FormatDuration(kv.Value)))));
                container.Children.Add(playtimeOnly.Build());
                return;
            }

            container.Children.Add(new TextBlock { Text = "No world data recorded." });
            return;
        }

        void RebuildSelf() => Rebuild(container, editor, onEdited, onMessage);

        // KnownWorlds (playtime, keyed by world *name*) enrichment:
        // fold "Time played" into a per-world block only when its
        // WorldDto.Identity.Name matches exactly one such block — two
        // different worlds can legitimately share a name, and guessing
        // wrong would misattribute another world's playtime. Any entry that
        // doesn't match unambiguously (including every remote-joined world,
        // which has no Identity at all) falls through to its own leftover
        // list below rather than being silently dropped.
        var namesWithSingleMatch = worlds
            .Where(w => w.Identity is not null)
            .GroupBy(w => w.Identity!.Name)
            .Where(g => g.Count() == 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var knownWorldsByName = data.KnownWorlds.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var matchedWorldNames = new HashSet<string>(StringComparer.Ordinal);

        var group = new RowGroup();
        foreach (var world in worlds)
        {
            // Unconditional, including the first world: the explanation
            // text above the group (IdentityEnrichmentExplanation) now
            // always occupies that "first line" position, so every world —
            // first included — needs its own divider to separate it from
            // whatever precedes it, unlike before that text existed.
            group.AddDivider();

            // Two buttons in the header's own button slot, not one: sharing
            // that slot's real button column with the "Has map data" row's »/✕
            // pair below (RowGroup.AddSectionHeader's own doc comment) is what
            // lines this row's controls up with that row's, position for
            // position — same width, same spacing, so "explore all" lands
            // above » and "remove world" lands above ✕, matching this pair's
            // own left-to-right order.
            var exploreAllButton = IconButtons.Create(
                IconButtons.ExploreAllGlyph,
                "Reveals this world's entire map (your own exploration only — doesn't affect map data shared with you).");
            exploreAllButton.IsEnabled = world.HasMapData;
            exploreAllButton.Click += (_, _) =>
            {
                editor.ExploreAllMap(world.WorldId);
                onMessage($"World {world.WorldId} fully explored");
                onEdited();
                RebuildSelf();
            };

            var removeWorldButton = IconButtons.Create(IconButtons.ClearGlyph, "Removes this world entry (only from this character — the world file itself is untouched).");
            removeWorldButton.Click += (_, _) =>
            {
                editor.RemoveWorld(world.WorldId);
                onMessage($"World {world.WorldId} removed");
                onEdited();
                RebuildSelf();
            };

            var headerButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            headerButtons.Children.Add(exploreAllButton);
            headerButtons.Children.Add(removeWorldButton);

            var header = world.Identity is not null
                ? $"World {world.WorldId} ({world.Identity.Name})"
                : $"World {world.WorldId}";
            group.AddSectionHeader(header, button: headerButtons);

            if (world.Identity is not null)
            {
                group.AddRow("Seed", string.IsNullOrEmpty(world.Identity.SeedName) ? "(none)" : world.Identity.SeedName);
                    
                if (world.Identity.SeenAsFiles.Count > 1)
                {
                    var otherNames = world.Identity.SeenAsFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                    group.AddRow("Also seen as", string.Join(", ", otherNames));
                }

                if (world.Identity.ConflictNote is not null)
                {
                    group.AddRow("Note", world.Identity.ConflictNote);
                }

                // Distinct provenance from Seed/"Also seen as"/"Note" above,
                // even though it's nested in the same Identity-resolved
                // block for the name lookup: those three come from scanning
                // local .fwl files (IdentityEnrichmentExplanation), while
                // this comes from the character's own save data
                // (WorldsDto.KnownWorlds, keyed by name).
                if (namesWithSingleMatch.Contains(world.Identity.Name)
                    && knownWorldsByName.TryGetValue(world.Identity.Name, out var secondsPlayed))
                {
                    group.AddRow("Time played", TabRows.FormatDuration(secondsPlayed));
                    matchedWorldNames.Add(world.Identity.Name);
                }
            }

            AddPointRow(
                group,
                "Spawn point",
                world.HaveCustomSpawnPoint ? Format(world.SpawnPoint) : "(default)",
                world.HaveCustomSpawnPoint,
                "Clears the custom spawn point, back to the world default.",
                () =>
                {
                    editor.ClearWorldSpawnPoint(world.WorldId);
                    onMessage($"Spawn point cleared for world {world.WorldId}");
                    onEdited();
                    RebuildSelf();
                });

            AddPointRow(
                group,
                "Logout point",
                world.HaveLogoutPoint ? Format(world.LogoutPoint) : "(none)",
                world.HaveLogoutPoint,
                "Clears the logout point.",
                () =>
                {
                    editor.ClearWorldLogoutPoint(world.WorldId);
                    onMessage($"Logout point cleared for world {world.WorldId}");
                    onEdited();
                    RebuildSelf();
                });

            group.AddRow("Death point", world.HaveDeathPoint ? Format(world.DeathPoint) : "(none)");

            var homeIsDefault = world.HomePoint is { X: 0, Y: 0, Z: 0 };
            AddPointRow(
                group,
                "Home point",
                Format(world.HomePoint),
                !homeIsDefault,
                "Clears the home point.",
                () =>
                {
                    editor.ClearWorldHomePoint(world.WorldId);
                    onMessage($"Home point cleared for world {world.WorldId}");
                    onEdited();
                    RebuildSelf();
                });

            var mapDataText = new TextBlock { Text = TabRows.FormatBool(world.HasMapData) };
            var viewMapButton = IconButtons.Create(IconButtons.ViewMapGlyph, "View this world's map.");
            viewMapButton.IsEnabled = world.HasMapData;
            viewMapButton.Click += async (_, _) => await ShowMap(editor, world, viewMapButton, onMessage);

            var clearMapButton = IconButtons.Create(IconButtons.ClearGlyph, "Clears cached map data (exploration and pins).");
            clearMapButton.IsEnabled = world.HasMapData;
            clearMapButton.Click += (_, _) =>
            {
                editor.ClearWorldMapData(world.WorldId);
                onMessage($"Map data cleared for world {world.WorldId} (exploration and pins both removed)");
                onEdited();
                RebuildSelf();
            };

            var mapButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            mapButtons.Children.Add(viewMapButton);
            mapButtons.Children.Add(clearMapButton);
            group.AddRow("Has map data", mapDataText, mapButtons);
        }

        // Leftover KnownWorlds entries: no per-world block to fold into,
        // either because the world was never locally identified (any
        // remote-joined world — WorldDto.Identity is null) or because its
        // name matched more than one per-world block (ambiguous, not
        // guessed at). Real, not hypothetical: confirmed against this
        // project's own corpus that these two
        // records commonly disagree in count, since they're independently
        // gated and keyed differently (WorldId vs. name).
        var leftoverKnownWorlds = data.KnownWorlds.Where(kv => !matchedWorldNames.Contains(kv.Key)).ToList();
        if (leftoverKnownWorlds.Count > 0)
        {
            group.AddDivider();
            group.AddSectionHeader("Known worlds (playtime)", leftoverKnownWorlds.Count);
            var pairs = leftoverKnownWorlds.Select(kv => (kv.Key, TabRows.FormatDuration(kv.Value)));
            group.AddRow(null, TabRows.BuildPairedList(pairs));
        }

        container.Children.Add(group.Build());
    }

    /// <summary>
    /// Resolves the owning <see cref="Window"/> from the clicked button
    /// itself via <see cref="TopLevel.GetTopLevel"/> rather than threading a
    /// window reference through <see cref="ITabModule.Build"/> — this tab is
    /// the first to need one at all (the still-open "Window-handle
    /// plumbing" note was about a confirmation dialog, not
    /// this), and the standard Avalonia lookup avoids widening every other
    /// tab module's signature for a need only this button has.
    /// </summary>
    private static async Task ShowMap(CharacterEditor editor, WorldDto world, Control sender, Action<string> onMessage)
    {
        var map = editor.DecodeWorldMap(world.WorldId);
        if (map is null)
        {
            onMessage("This world's map data could not be decoded.");
            return;
        }

        if (TopLevel.GetTopLevel(sender) is not Window owner)
        {
            return;
        }

        await WorldMapWindow.Open(owner, world, map);
    }

    /// <summary>
    /// A point row with a ✕ that clears it — same manual-button shape as
    /// "Remove world"/"Clear map data" above (a full <see cref="Rebuild"/>
    /// on click, not <see cref="RowGroup.AddClearableFlagRow"/>'s
    /// self-updating text, since these rows show a coordinate string, not a
    /// bool). No per-row description: real in-game consequences (a lost
    /// spawn point stranding you far from a portal on death, etc.) are
    /// stated once, in <see cref="ClearActionsLegendExplanation"/>, not
    /// repeated on every world's copy of the same rows — see this class's
    /// own doc comment for why that repetition stopped being worth it.
    /// <paramref name="tooltip"/> only states the mechanical fact (what
    /// clearing *this* point sets it back to); it doesn't restate the
    /// legend's warning.
    /// </summary>
    private static void AddPointRow(
        RowGroup group,
        string label,
        string valueText,
        bool canClear,
        string tooltip,
        Action onClear)
    {
        var button = IconButtons.Create(IconButtons.ClearGlyph, tooltip);
        button.IsEnabled = canClear;
        button.Click += (_, _) => onClear();

        group.AddRow(label, new TextBlock { Text = valueText }, button);
    }

    private static string Format(PositionDto position) => $"({position.X:0.#}, {position.Y:0.#}, {position.Z:0.#})";
}
