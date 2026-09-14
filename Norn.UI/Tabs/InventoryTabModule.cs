using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The 8×4 inventory grid. Loki's own grid was consulted directly
/// as a functionality/visuals target, not a build template — see that
/// document for where Norn's mechanism deliberately diverges and why.
/// <para>
/// Action model, revised again after the tile-interaction pass:
/// right-click opens a context menu (Repair /
/// Quality Up / Quality Down / Fill Stack / Delete); left-click on a tile
/// fires whichever single quick action the item actually supports (repair or
/// fill-stack — never both), cued by the tile's own cursor changing to a hand
/// when one applies; Ctrl+left-click deletes outright, mirroring the game's
/// own drop gesture rather than being a Norn invention. No hover highlighting
/// and no per-tile keyboard shortcuts — both were found to rest on
/// an implicit "what's the target" signal that didn't hold up.
/// </para>
/// <para>
/// Manual numeric entry (stack count, durability) is gone entirely, not
/// redesigned — the popup it used to go through (<c>StackEditDialog</c>) had
/// real chrome/layout problems, and an in-place textbox was weighed and set
/// aside too: Avalonia's own "this is editable" visual treatment doesn't sit
/// well in a tile this small without crowding it or breaking alignment with
/// the surrounding static text (the same tension already visible in
/// <c>General</c>'s player-name row, worse at tile scale). Quality, stack,
/// and durability are all reachable only through the same stepped/maximize
/// actions everything else in this tab uses. A well-designed inline control
/// for this remains a real future direction, just not one built for this
/// pass.
/// </para>
/// <para>
/// Every direct action now posts a short status-bar note via <c>onMessage</c>
/// (<see cref="StatusBar.ShowMessage"/>) — closes the "did that actually
/// happen?" gap that direct-fire actions (especially left-click) had with no
/// feedback at all. Scoped to this tab only for now; whether other tabs adopt
/// it is a separate, deliberately deferred pass.
/// </para>
/// <para>
/// Not in this slice: browsing/adding a new item, and toggling equip state
/// (read-only equippable classification only, no write path).
/// Shift+left-click stack-splitting (the game's own gesture) was considered
/// alongside Ctrl+left-click delete and deliberately deferred — it implies a
/// drag-and-drop landing mechanism Norn has nowhere else, and a split with no
/// sane landing rule would be a worse feature than no split at all.
/// </para>
/// </summary>
public sealed class InventoryTabModule : ITabModule
{
    public string Title => "Inventory";

    public Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(16) };
        var container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
        panel.Children.Add(container);

        Rebuild(container, editor, onEdited, onMessage);

        return new ScrollViewer { Content = panel };
    }

    private static void Rebuild(StackPanel container, CharacterEditor editor, Action onEdited, Action<string> onMessage)
    {
        container.Children.Clear();

        var items = editor.View.Inventory.Items;

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var repairableCount = items.Count(CanRepair);
        var repairAll = new Button { Content = "Repair All" };
        repairAll.IsEnabled = repairableCount > 0;
        repairAll.Click += (_, _) =>
        {
            editor.RepairAllItems();
            onMessage($"Repaired {repairableCount} {(repairableCount == 1 ? "item" : "items")}");
            onEdited();
            Rebuild(container, editor, onEdited, onMessage);
        };

        var fillableCount = items.Count(CanFillStack);
        var fillAll = new Button { Content = "Fill All Stacks" };
        fillAll.IsEnabled = fillableCount > 0;
        fillAll.Click += (_, _) =>
        {
            editor.FillAllStacks();
            onMessage($"Filled {fillableCount} {(fillableCount == 1 ? "stack" : "stacks")}");
            onEdited();
            Rebuild(container, editor, onEdited, onMessage);
        };

        var addItem = new Button { Content = "Add Item" };
        var emptySlot = FindFirstEmptySlot(items);
        addItem.IsEnabled = emptySlot is not null;
        addItem.Click += async (_, _) =>
        {
            var (slotX, slotY) = emptySlot!.Value;
            await AddItem(slotX, slotY, editor, addItem, onEdited, onMessage, () => Rebuild(container, editor, onEdited, onMessage));
        };

        buttonRow.Children.Add(repairAll);
        buttonRow.Children.Add(fillAll);
        buttonRow.Children.Add(addItem);
        container.Children.Add(buttonRow);

        container.Children.Add(BuildGrid(editor, () => Rebuild(container, editor, onEdited, onMessage), onEdited, onMessage));
    }

    // Logical, content-independent tile edge length. Fixed (not Min) and paired
    // with the Viewbox below: a tile's actual desired size used to be driven by
    // its own content (a long/wrapping display name could ask for more height
    // than 96px), which fed into UniformGrid's per-cell sizing and from there
    // into AspectRatioBox's own reported size — so the whole grid visibly
    // changed size switching between save files with different item names, even
    // at a fixed window size. Pinning the grid's Width/Height makes its natural
    // size exactly 96px/tile regardless of content; the Viewbox then does the
    // window-responsive scaling AspectRatioBox still needs, uniformly, so
    // per-tile content never gets a vote in how big the grid or its tiles are.
    private const double TileSize = 96;

    private static Control BuildGrid(CharacterEditor editor, Action rebuild, Action onEdited, Action<string> onMessage)
    {
        // Last-one-wins rather than ToDictionary (found in review): two items
        // can legitimately occupy the same grid position in a real save, and
        // the read path preserves them deliberately rather than dropping one,
        // so this display-side grouping must not be the thing that throws.
        // ToDictionary threw on the duplicate key, which — because tabs are
        // built and swapped in one at a time — left earlier tabs showing the
        // newly selected character while later ones still showed the previous
        // one. The tile below can only render one item per cell regardless;
        // which one it picks is arbitrary either way, and no longer fatal.
        var byPosition = new Dictionary<(int, int), ItemDto>();
        foreach (var item in editor.View.Inventory.Items)
        {
            byPosition[(item.GridX, item.GridY)] = item;
        }

        var grid = new UniformGrid
        {
            Columns = InventoryLayout.Width,
            Rows = InventoryLayout.Height,
            Width = InventoryLayout.Width * TileSize,
            Height = InventoryLayout.Height * TileSize,
        };

        for (var y = 0; y < InventoryLayout.Height; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                byPosition.TryGetValue((x, y), out var item);
                grid.Children.Add(BuildTile(x, y, item, editor, rebuild, onEdited, onMessage));
            }
        }

        return new AspectRatioBox
        {
            AspectRatio = (double)InventoryLayout.Width / InventoryLayout.Height,
            Child = new Viewbox { Child = grid },
        };
    }

    private static Control BuildTile(int x, int y, ItemDto? item, CharacterEditor editor, Action rebuild, Action onEdited, Action<string> onMessage)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Margin = new Thickness(2),
            // Two-state, matching the game's own inventory: equipped vs. not —
            // no distinction for "could be equipped but isn't" (that was a
            // Norn-only third state that made the tab feel less like the game
            // it's mirroring; dropped as part of this redesign).
            Background = item switch
            {
                null => Brushes.DimGray,
                { Equipped: true } => Brushes.SlateGray,
                _ => Brushes.Gray,
            },
            ClipToBounds = true,
        };

        var content = new Grid();

        if (item is null)
        {
            content.Children.Add(new TextBlock
            {
                Text = "EMPTY",
                Opacity = 0.25,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });

            border.Child = content;

            var emptyMenu = new ContextMenu();
            var addHere = new MenuItem { Header = "Add item" };
            addHere.Click += async (_, _) => await AddItem(x, y, editor, border, onEdited, onMessage, rebuild);
            emptyMenu.Items.Add(addHere);
            border.ContextMenu = emptyMenu;

            return border;
        }

        var shared = SharedItemDataCatalog.TryFind(item.PrefabName);
        var displayName = shared?.DisplayName ?? item.PrefabName;

        // Three rows so the name's wrapped text can never grow into the
        // quality badge or the bottom durability/stack readout: each Auto
        // row claims exactly what its own content needs (zero, if that
        // content is absent) before the middle Star row gets whatever's
        // actually left over. Previously all three were free-floating in one
        // cell, kept apart only by a guessed fixed Margin on the name — found
        // insufficient by a long, durability-bar-paired name ("Thundering
        // Berserkr Axes") whose wrapped text grew past that margin and
        // visually overlapped the bar beneath it.
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        border.Child = content;

        // The name's real available width, straight from Avalonia's own
        // layout engine rather than hand-arithmetic on TileSize/Padding/
        // Margin — an offline Measure/Arrange probe against the already-real
        // Border (same Padding/Margin it'll actually render with) at the
        // tile's actual native size.
        border.Measure(new Size(TileSize, TileSize));
        border.Arrange(new Rect(0, 0, TileSize, TileSize));
        // A 10% safety margin, not the raw measured value: logging actual
        // widths while running the app found the hand-derived and
        // engine-measured numbers agreed (both 76) — the real gap was
        // "Berserkir Axes" measuring ~74.5 via FormattedText (what
        // TextMeasurement/PackLine use below) while TextBlock's own
        // TextLayout wrap engine — a different measurement code path
        // internally — decided it didn't fit. A few percent of headroom
        // absorbs that kind of cross-measurement-path rounding rather than
        // chasing sub-pixel parity between two different text-measurement
        // systems.
        var nameLineWidth = (content.Bounds.Width - 8) * 0.9; // less the name TextBlock's own horizontal Margin

        if (shared is { MaxQuality: > 1 })
        {
            var badge = new TextBlock
            {
                Text = $"{item.Quality}",
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetRow(badge, 0);
            content.Children.Add(badge);
        }

        var nameText = new TextBlock
        {
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            // MaxLines/TextTrimming stay as a hard backstop against the
            // TruncateName heuristic below undershooting — they cap the
            // control's own height regardless, which is what actually
            // prevents an overlap with a neighboring row. They're not what
            // puts "…" on screen, though: Avalonia doesn't render
            // TextTrimming's ellipsis on the cut line when TextWrapping is
            // also Wrap (confirmed by running the app — content just silently
            // drops past MaxLines, no glyph) — see TruncateName for where
            // the actual visible "…" comes from.
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 2),
        };
        nameText.Text = TruncateName(nameText, displayName, nameLineWidth);
        Grid.SetRow(nameText, 1);
        content.Children.Add(nameText);

        var bottom = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 2,
        };
        Grid.SetRow(bottom, 2);

        if (shared is { UsesDurability: true })
        {
            var max = shared.MaxDurabilityFor(item.Quality);
            // Defensive: a raw stored durability shouldn't exceed the
            // catalog-derived max, but clamp the displayed value regardless
            // rather than trust that invariant blindly.
            var clamped = Math.Clamp(item.Durability, 0f, (float)max);

            bottom.Children.Add(new TextBlock
            {
                Text = $"{clamped:0} / {max:0}",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            bottom.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = max,
                Value = clamped,
                Height = 6,
                // Fluent's default ProgressBar carries a MinWidth sized for
                // typical usage, larger than an inventory tile — it silently
                // overflows the tile and swallows any Margin unless reset to
                // 0 (same class of theme-default fix as the Slider one).
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(6, 0, 6, 4),
            });
        }
        else if (shared is { MaxStack: > 1 })
        {
            // Plain, non-interactive readout — manual stack entry is gone
            // (see the type doc); this is display only, same shape as the
            // durability text above it.
            bottom.Children.Add(new TextBlock
            {
                Text = $"{item.Stack} / {shared.MaxStack}",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        content.Children.Add(bottom);

        ToolTip.SetTip(border, BuildTooltip(displayName, item, shared));

        // Hand cursor only, no tooltip hint for what a click does — cursor is
        // the honest "something's interactive here" signal (same category as
        // the read-only equip glyph reasoning, applied to affordance
        // instead); a predicted-outcome line would mix "facts about the item"
        // with "what a click will do" in the one tooltip whose content is
        // already settled.
        var canRepair = CanRepair(item);
        var canFillStack = CanFillStack(item);
        if (canRepair || canFillStack)
        {
            border.Cursor = new Cursor(StandardCursorType.Hand);
        }

        border.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Ctrl+left-click deletes outright — the game's own drop gesture,
            // borrowed for recognition rather than invented; always available,
            // same as the menu's Delete, no CanX gate.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                editor.RemoveItemAt(x, y);
                onMessage($"Deleted {displayName}");
                onEdited();
                rebuild();
                return;
            }

            if (canRepair)
            {
                editor.RepairItemAt(x, y);
                onMessage($"Repaired {displayName}");
            }
            else if (canFillStack)
            {
                editor.FillItemStack(x, y);
                onMessage($"Filled {displayName} to {shared!.MaxStack}");
            }
            else
            {
                return;
            }

            onEdited();
            rebuild();
        };

        border.ContextMenu = BuildContextMenu(x, y, item, displayName, shared, editor, rebuild, onEdited, onMessage);

        return border;
    }

    private const string Ellipsis = "…";

    /// <summary>
    /// Simulates <c>TextWrapping.Wrap</c> greedily packing <paramref
    /// name="text"/>'s words into two lines at <paramref name="lineWidth"/>
    /// each (a real measured width — see <see cref="BuildTile"/>'s
    /// Measure/Arrange probe, not a hand-derived constant: an arithmetic
    /// first attempt at that number overestimated the real available width
    /// enough to under-truncate "Thundering Berserkir Axes", found by
    /// logging actual measured word widths while running the app, not by
    /// eyeballing the result), and if words remain past that,
    /// shortens the second line until "&lt;line2&gt;…" itself fits — the
    /// actual visible truncation mark for the tile-face name (see
    /// <see cref="BuildTile"/>'s comment on why Avalonia's own
    /// <c>TextTrimming</c> doesn't render one here).
    /// <para>
    /// Deliberately per-line, not a single aggregate two-line budget: a
    /// first attempt at this compared the whole candidate's width against
    /// `lineWidth * 2` in one shot (<see cref="TextMeasurement"/>,
    /// binary-search style, same shape as <see cref="StatusBar.Compact"/>'s
    /// path truncation) — the aggregate budget can pass even when
    /// "Berserkir…", glued into one unbreakable token with no space to wrap
    /// at, individually overflows its own line and gets pushed to a third,
    /// MaxLines-clipped line — the exact failure this whole method exists to
    /// avoid. Packing line-by-line and re-checking the ellipsis's own fit
    /// avoids that by construction.
    /// </para>
    /// </summary>
    private static string TruncateName(TextBlock reference, string text, double lineWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line1 = "";
        var line2 = "";
        var i = 0;

        i = PackLine(reference, words, i, lineWidth, ref line1);
        i = PackLine(reference, words, i, lineWidth, ref line2);

        if (i >= words.Length)
        {
            return text;
        }

        while (line2.Length > 0 && TextMeasurement.Width(reference, line2 + Ellipsis) > lineWidth)
        {
            var lastSpace = line2.LastIndexOf(' ');
            line2 = lastSpace < 0 ? "" : line2[..lastSpace];
        }

        var tail = line2.Length == 0 ? Ellipsis : line2 + Ellipsis;
        return line1.Length == 0 ? tail : $"{line1} {tail}";
    }

    /// <summary>
    /// Greedily appends words (starting at <paramref name="start"/>) onto
    /// <paramref name="line"/> while they still fit <paramref
    /// name="lineWidth"/> — always accepts at least one word even if it
    /// alone overflows, same as real word-wrap, which can't avoid placing an
    /// oversized word somewhere. Returns the index of the first word not
    /// packed in.
    /// </summary>
    private static int PackLine(TextBlock reference, string[] words, int start, double lineWidth, ref string line)
    {
        var i = start;
        while (i < words.Length)
        {
            var candidate = line.Length == 0 ? words[i] : line + " " + words[i];
            if (line.Length != 0 && TextMeasurement.Width(reference, candidate) > lineWidth)
            {
                break;
            }

            line = candidate;
            i++;
        }

        return i;
    }

    private static ContextMenu BuildContextMenu(int x, int y, ItemDto item, string displayName, SharedItemDataDto? shared, CharacterEditor editor, Action rebuild, Action onEdited, Action<string> onMessage)
    {
        var menu = new ContextMenu();

        void Add(string header, bool enabled, Action action)
        {
            var menuItem = new MenuItem { Header = header, IsEnabled = enabled };
            menuItem.Click += (_, _) =>
            {
                action();
                onEdited();
                rebuild();
            };
            menu.Items.Add(menuItem);
        }

        // Quality up/down post no message — the quality badge sits on the
        // tile face itself and updates in the same glance, unlike
        // repair/fill/delete, which had nothing on the tile to show a change
        // happened at all.
        Add("Repair", CanRepair(item), () => { editor.RepairItemAt(x, y); onMessage($"Repaired {displayName}"); });
        Add("Quality up", CanQualityUp(item), () => editor.SetItemQuality(x, y, item.Quality + 1));
        Add("Quality down", CanQualityDown(item), () => editor.SetItemQuality(x, y, item.Quality - 1));
        Add("Fill stack", CanFillStack(item), () => { editor.FillItemStack(x, y); onMessage($"Filled {displayName} to {shared!.MaxStack}"); });
        Add("Set crafter to self", CanSetCrafterToSelf(item, editor), () => { editor.SetItemCrafterAt(x, y); onMessage($"Marked {displayName} as crafted by you"); });
        Add("Clear crafter tag", CanClearCrafter(item), () => { editor.ClearItemCrafterAt(x, y); onMessage($"Cleared crafter tag on {displayName}"); });
        Add("Delete", true, () => { editor.RemoveItemAt(x, y); onMessage($"Deleted {displayName}"); });

        return menu;
    }

    /// <summary>
    /// Whole-tile hover tooltip — the "how do we show
    /// crafter/weight without a persistent on-tile glyph" answer, matching
    /// the game's own single-info-card-on-hover shape rather than Norn's
    /// usual always-visible-text convention. Aggregates the full display
    /// name, crafter, cheated status, equipped status, and weight; deferred
    /// per-tile enrichment (item description, etc.) can extend this later
    /// without a new mechanism.
    /// <para>
    /// The name line is always included, not just when the tile face
    /// truncates it (<see cref="TextBlock.MaxLines"/>, above) — same
    /// "always shown, not truncation-conditional" call already made for
    /// the sidebar's file-row tooltip in <c>MainWindow.BuildSidebarRow</c>,
    /// for the same reason: detecting "did it actually truncate" isn't
    /// worth the complexity when just always showing it is simpler and
    /// costs nothing.
    /// </para>
    /// <para>
    /// The equipped line reuses <see cref="ItemEquippability"/>, kept
    /// dormant since the three-state tile background was dropped — this
    /// is that classification's first real consumer, now surfacing
    /// equipped/not for equipable items inside the tooltip rather than as a
    /// persistent tile-face signal.
    /// </para>
    /// </summary>
    private static string BuildTooltip(string displayName, ItemDto item, SharedItemDataDto? shared)
    {
        var lines = new List<string> { displayName };

        // The prefab name, second, and only when it isn't already the heading.
        //
        // Two reasons it earns a line. It is the identifier the game's own console takes
        // ("spawn ArmorTrollLeatherChest"), so a reader who wants to act on what they are
        // looking at needs exactly this string. And from Valheim 1.0 it is the only thing
        // that tells same-named items apart: troll leather armour exists three times over
        // under one display name, and the tile would otherwise give no way to know which
        // one is in the slot.
        //
        // Skipped when displayName already IS the prefab name, which is what the caller
        // falls back to for an item the catalog cannot resolve — repeating it would be
        // noise, and the absence is itself the signal that it went unresolved.
        if (!string.IsNullOrEmpty(item.PrefabName) && item.PrefabName != displayName)
        {
            lines.Add(item.PrefabName);
        }

        if (!string.IsNullOrEmpty(item.CrafterName))
        {
            lines.Add($"Crafted by: {item.CrafterName}");
        }

        // Read-only — surfacing an already-true fact from the save, per
        // CLAUDE.md §4 item 9, same category as Unlockables' Trophies field.
        // Shown only when true: the overwhelming majority of items were
        // never console-spawned, so showing this unconditionally would be
        // noise on nearly every tile — same "notable case only" convention
        // PickedUp uses below, just inverted (there, false is notable; here,
        // true is).
        if (item.Cheated)
        {
            lines.Add("Cheated: Yes");
        }

        if (shared is not null && ItemEquippability.IsEquipable(shared.ItemType))
        {
            lines.Add($"Equipped: {TabRows.FormatBool(item.Equipped)}");
        }

        if (shared is not null)
        {
            var unit = shared.WeightFor(item.Quality);
            lines.Add(shared.MaxStack > 1
                ? $"Weight: {unit:0.0} ({unit * item.Stack:0.0} Total)"
                : $"Weight: {unit:0.0}");
        }

        // Scalar facts above, list-shaped content last — a fixed
        // yes/no fact (PickedUp) still sorts before a variable-length list
        // (CustomData) within that "new content" tier. PickedUp only shown
        // when false: true is the overwhelming default for anything already
        // sitting in an inventory (it marks "ever picked up," relevant to a
        // world-dropped item's own despawn timer, not inventory state), so
        // showing it unconditionally would be noise on nearly every tile.
        if (!item.PickedUp)
        {
            lines.Add("Picked up: No");
        }

        if (item.CustomData.Count > 0)
        {
            lines.Add("Custom data: " + string.Join(", ", item.CustomData.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        return string.Join("\n", lines);
    }

    private static bool CanRepair(ItemDto item) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { UsesDurability: true } shared
        && item.Durability < shared.MaxDurabilityFor(item.Quality);

    private static bool CanFillStack(ItemDto item) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { MaxStack: > 1 } shared
        && item.Stack < shared.MaxStack;

    private static bool CanQualityUp(ItemDto item) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { MaxQuality: > 1 } shared
        && item.Quality < shared.MaxQuality;

    private static bool CanQualityDown(ItemDto item) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { MaxQuality: > 1 }
        && item.Quality > 1;

    /// <summary>Row-major scan (matches <see cref="BuildGrid"/>'s own
    /// iteration order) for the first unoccupied slot, or <c>null</c> if the
    /// grid is full. Drives the toolbar "Add Item" button's target slot and
    /// its own enabled state.</summary>
    private static (int X, int Y)? FindFirstEmptySlot(IReadOnlyList<ItemDto> items)
    {
        var occupied = items.Select(i => (i.GridX, i.GridY)).ToHashSet();

        for (var y = 0; y < InventoryLayout.Height; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Shared behavior behind both Add Item entry points (the toolbar button
    /// and an empty tile's own context menu): resolves the owning
    /// <see cref="Window"/> from whichever control was clicked, the same
    /// pattern <c>WorldsTabModule.ShowMap</c> established for its own
    /// on-demand modal, opens the picker, and applies its result.
    /// </summary>
    private static async Task AddItem(int x, int y, CharacterEditor editor, Control sender, Action onEdited, Action<string> onMessage, Action rebuild)
    {
        if (TopLevel.GetTopLevel(sender) is not Window owner)
        {
            return;
        }

        var result = await AddItemWindow.Open(owner);
        if (result is null)
        {
            return;
        }

        editor.AddItemAt(x, y, result.Value.PrefabName);

        var displayName = SharedItemDataCatalog.TryFind(result.Value.PrefabName)?.DisplayName ?? result.Value.PrefabName;

        if (result.Value.SetCrafter)
        {
            editor.SetItemCrafterAt(x, y);
        }

        if (result.Value.FillStack)
        {
            editor.FillItemStack(x, y);
        }

        var qualifiers = new List<string>();
        if (result.Value.SetCrafter)
        {
            qualifiers.Add("crafted by you");
        }

        if (result.Value.FillStack)
        {
            qualifiers.Add("stack filled");
        }

        var suffix = qualifiers.Count > 0 ? $" ({string.Join(", ", qualifiers)})" : "";
        onMessage($"Added {displayName}{suffix}");

        onEdited();
        rebuild();
    }

    // Gated on SharedItemDataCatalog, superseding this note's own earlier
    // claim: the real game does restrict
    // which items can ever receive a crafter tag (only a crafting-bench
    // recipe's own m_item output; cooked/smelted/fermented/drop-only items
    // never can) once that per-item fact was resolved against the game's
    // own recipe data.
    // An unresolved item (not in the catalog at all) defaults to allowed,
    // not blocked — matching SharedItemDataDto's own "unknown must not
    // silently become unsupported" rule for a missing CSV column.
    private static bool CanSetCrafterToSelf(ItemDto item, CharacterEditor editor) =>
        (SharedItemDataCatalog.TryFind(item.PrefabName)?.CanHaveCrafterTag ?? true)
        && item.CrafterId != editor.View.Meta.PlayerId;

    private static bool CanClearCrafter(ItemDto item) => item.CrafterId != 0;
}
