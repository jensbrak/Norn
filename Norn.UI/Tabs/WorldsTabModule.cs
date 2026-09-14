using Avalonia.Controls;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>Per-world spawn/logout/death/home coordinates, a "View map"
/// button, and "Remove world" (<see cref="CharacterEditor.RemoveWorld"/> —
/// drops this character's own record of the world, never the world file
/// itself). "Clear map data", "Clear received map data", and "Reveal
/// world" (formerly "Explore All") all live in <see cref="WorldMapWindow"/>
/// instead, next to the view that shows what they'd affect — see that
/// type's doc comment. No ✕ here prompts for confirmation, matching Vitals'
/// per-row food removal.
/// <para>
/// "Remove world" sits on the section header itself
/// (<see cref="RowGroup.AddSectionHeader"/>'s <c>button</c> param) rather
/// than its own row — self-explanatory once it's right next to the world's
/// name. What each ✕ on this tab actually does in-game is stated once, in
/// <see cref="ClearActionsLegendExplanation"/> above the whole list, not
/// repeated per row.
/// </para>
/// <para>
/// Self-rebuilding, same shape as <c>InventoryTabModule</c>: <see cref="Build"/>
/// wires a container once, and every mutating button calls <c>Rebuild</c>
/// on it afterward — <c>onEdited</c> is chrome-only and never touches tab
/// content on its own.
/// </para>
/// <para>
/// One shared <see cref="RowGroup"/> across every world, with a
/// <see cref="RowGroup.AddDivider"/> before each — not one
/// <see cref="RowGroup"/> per world, which let each world's data-column
/// width drift independently once the "View map" button entered it.
/// </para>
/// <para>
/// A world's header grows a parenthetical resolved name (e.g. "World
/// 3725283970 (Hearthhold)") plus a "Seed" row when <see cref="WorldDto.Identity"/>
/// is known — purely local enrichment from scanning nearby <c>.fwl</c>
/// files, absent when nothing matched. "Also seen as" lists other local
/// filenames this UID has been scanned under (shown only when more than
/// one); "Note" flags a name/seed conflict. "Time played" folds in from
/// <c>WorldsDto.KnownWorlds</c> (keyed by name, independently of
/// <c>Identity</c>) only when exactly one world matches that name
/// unambiguously — ambiguous or unmatched entries get their own leftover
/// list at the bottom instead of being guessed at.
/// </para>
/// <para>
/// Spawn/logout/home each carry a ✕ (<see cref="AddPointRow"/>) that clears
/// them; death point has none — nothing in the game ever reads that field.
/// Each ✕'s own tooltip states only the mechanical fact ("clears the spawn
/// point"); real in-game consequences are covered once, in
/// <see cref="ClearActionsLegendExplanation"/> above the list, not per row.
/// The same four points, plus a legend, also appear on
/// <see cref="WorldMapWindow"/>'s map view via its own "Show points" toggle.
/// </para>
/// <para>
/// <see cref="IdentityEnrichmentExplanation"/> sits once above the whole
/// list — without it, a user seeing name/seed on some worlds and not
/// others could read the gap as a bug rather than "bonus info Norn found
/// locally." Shown whenever the tab has any worlds, not only when
/// something's unresolved, so its own presence never looks like a change.
/// </para></summary>
public sealed class WorldsTabModule : ITabModule
{
    public string Title => "Worlds";

    private const string IdentityEnrichmentExplanation =
        "Names and seeds shown below come from matching local world files on this computer. "
        + "Worlds played by joining hosted games by others will not show additional info.";

    /// <summary>
    /// A blunt warning, not a teaching paragraph — how much explanation
    /// "clearing this point" needs depends on what the reader already
    /// knows, so no single paragraph length serves both a new and an
    /// experienced player. A short warning is right-sized for everyone; the
    /// real mechanics belong on a future Help surface (not built yet), not
    /// here. "Death point has no known use in game" doubles as this row's
    /// reason for having no ✕. Covers all three ✕ actions on this tab with
    /// one "current character only" clause, including "Remove world" (which
    /// still never touches the actual world file). Dismissible through
    /// Settings (<see cref="Settings.ShowWorldsRemovalWarning"/>), same as
    /// <see cref="IdentityEnrichmentExplanation"/>
    /// (<see cref="Settings.ShowWorldsIdentityInfo"/>) — independently, and
    /// recoverable if dismissed by mistake.
    /// </summary>
    private const string ClearActionsLegendExplanation =
        "Point removal only affects the current character, but please make sure you understand how "
        + "it affects your character. Death point has no known use in game.";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Avalonia.Thickness(16) };

        if (SettingsStore.Current.ShowWorldsIdentityInfo)
        {
            panel.Children.Add(TabRows.BuildDismissibleLegend(
                IdentityEnrichmentExplanation,
                () => SettingsStore.Current.ShowWorldsIdentityInfo = false));
        }

        if (SettingsStore.Current.ShowWorldsRemovalWarning)
        {
            panel.Children.Add(TabRows.BuildDismissibleLegend(
                ClearActionsLegendExplanation,
                () => SettingsStore.Current.ShowWorldsRemovalWarning = false));
        }

        var container = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(container);

        Rebuild(container, editor, onEdited, onMessage);

        return new ScrollViewer { Content = panel };
    }

    private static void Rebuild(StackPanel container, CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        container.Children.Clear();

        var data = editor.View.Worlds;
        var worlds = data.Worlds;

        if (worlds.Count == 0)
        {
            // KnownWorlds survives removing every per-world entry, so it
            // still needs rendering here rather than being hidden by the
            // early return below.
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

        // Fold "Time played" into a per-world block only when its
        // Identity.Name matches exactly one KnownWorlds entry — ambiguous
        // or nameless (remote-joined) worlds fall through to the leftover
        // list below instead of being guessed at.
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
            // Unconditional (including the first world): the explanation
            // text above the group always occupies the "first line"
            // position, so every world needs its own divider.
            group.AddDivider();

            // Lone header button — lines up with the "Has map data" row's
            // single » below (RowGroup.AddSectionHeader's own doc comment).
            var removeWorldButton = IconButtons.Create(IconButtons.ClearGlyph, "Removes this world entry (only from this character — the world file itself is untouched).");
            removeWorldButton.Click += (_, _) =>
            {
                editor.RemoveWorld(world.WorldId);
                onMessage($"World {world.WorldId} removed");
                onEdited();
                RebuildSelf();
            };

            var header = world.Identity is not null
                ? $"World {world.WorldId} ({world.Identity.Name})"
                : $"World {world.WorldId}";
            group.AddSectionHeader(header, button: removeWorldButton);

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

                // Unlike Seed/"Also seen as"/"Note" above, this comes from
                // the save itself (KnownWorlds), not local .fwl scanning.
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
            viewMapButton.Click += async (_, _) => await ShowMap(editor, world, viewMapButton, onEdited, onMessage, RebuildSelf);

            group.AddRow("Has map data", mapDataText, viewMapButton);
        }

        // Leftover KnownWorlds entries with no per-world block to fold
        // into: never locally identified, or an ambiguous name match.
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
    /// Resolves the owning <see cref="Window"/> via <see cref="TopLevel.GetTopLevel"/>
    /// rather than threading one through <see cref="ITabModule.Build"/>,
    /// since this is the only button that needs it. <see cref="WorldMapWindow"/>
    /// can now mutate state (its map-data edits), so <paramref name="rebuild"/>
    /// always runs once the modal closes — cheaper than tracking which of
    /// its actions, if any, actually fired.
    /// </summary>
    private static async Task ShowMap(CharacterEditor editor, WorldDto world, Control sender, Action onEdited, Action<string> onMessage, Action rebuild)
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

        await WorldMapWindow.Open(owner, editor, world, map, onEdited, onMessage);
        rebuild();
    }

    /// <summary>
    /// A point row with a ✕ that clears it — always a full <see cref="Rebuild"/>
    /// on click, not <see cref="RowGroup.AddClearableFlagRow"/>'s
    /// self-updating text, since these rows show a coordinate, not a bool.
    /// <paramref name="tooltip"/> states only the mechanical fact; real
    /// consequences are covered once by <see cref="ClearActionsLegendExplanation"/>,
    /// not per row.
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
