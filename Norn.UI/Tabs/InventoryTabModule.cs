using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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
/// Quality Up / Quality Down / Fill Stack / Edit Amount / Delete);
/// left-click on a tile
/// fires whichever single quick action the item actually supports (repair or
/// fill-stack — never both), cued by the tile's own cursor changing to a hand
/// when one applies; Ctrl+left-click deletes outright, mirroring the game's
/// own drop gesture rather than being a Norn invention. No hover highlighting
/// and no per-tile keyboard shortcuts — both were found to rest on
/// an implicit "what's the target" signal that didn't hold up.
/// </para>
/// <para>
/// Exact stack entry is back, quality and durability remain
/// stepped/maximize-only. The original manual entry (stack count and
/// durability both) was removed entirely in an earlier pass — the popup it
/// went through (<c>StackEditDialog</c>) had real chrome/layout problems, and
/// an in-place textbox was weighed and set aside too: Avalonia's own "this is
/// editable" visual treatment doesn't sit well in a tile this small without
/// crowding it or breaking alignment with the surrounding static text (the
/// same tension already visible in <c>General</c>'s player-name row, worse
/// at tile scale). That earlier pass also recorded a sketch for a future
/// direction: a field that reads as ordinary static text until
/// hovered/clicked, with no frame present at rest. "Edit amount" here is a
/// deliberate **substitution** for that sketch, not an implementation of
/// it — a light-dismiss <c>Flyout</c> reached via the context menu, not an
/// always-present inline control — chosen because it reuses this app's
/// existing "closing is declining" popup convention and needed no new
/// always-present-control design work. It also avoids the earlier pass's
/// specific chrome complaints structurally: the amount field is a plain
/// <see cref="NumericUpDown"/> (<c>Minimum</c>/<c>Maximum</c>/<c>Value</c>
/// only, no <c>InnerRightContent</c> "/ M" trick — the max is shown as
/// ordinary separate static text instead), and the popup is a non-modal
/// <c>Flyout</c>, not a <c>Window</c>, so there's no minimize button or
/// title bar to get wrong.
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

    /// <summary>
    /// Category-filtered quick-fill toolbar buttons — each a subset of "Fill
    /// All Stacks" scoped to a fixed set of <see cref="ItemType"/>s. Kept as a
    /// small data table rather than two bespoke button blocks so a future
    /// "N configurable quick-fill buttons" extension (Settings-driven
    /// label/category mapping) is a data change here, not a redesign.
    /// </summary>
    private static readonly (string Label, ItemType[] Types)[] QuickFillPresets =
    [
        ("Rearm", [ItemType.Ammo, ItemType.AmmoNonEquipable]),
        ("Restock", [ItemType.Consumable]),
    ];

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
        addItem.IsEnabled = emptySlot is not null || items.Any(CanFillStack);
        addItem.Click += async (_, _) =>
        {
            // No anchor — the toolbar button never targets one specific
            // tile, unlike an empty tile's own "Add item" menu entry below.
            // A stackable item merges/fills wherever it fits; a
            // non-stackable one falls back to a freshly found empty slot
            // inside AddItem itself.
            await AddItem(null, editor, addItem, onEdited, onMessage, () => Rebuild(container, editor, onEdited, onMessage));
        };

        buttonRow.Children.Add(repairAll);
        buttonRow.Children.Add(fillAll);

        foreach (var (label, types) in QuickFillPresets)
        {
            var typeSet = new HashSet<ItemType>(types);
            var quickFillCount = items.Count(i => CanFillStackOfType(i, typeSet));
            var quickFill = new Button { Content = label };
            quickFill.IsEnabled = quickFillCount > 0;
            quickFill.Click += (_, _) =>
            {
                editor.FillStacksOfType(typeSet);
                onMessage($"Filled {quickFillCount} {(quickFillCount == 1 ? "stack" : "stacks")}");
                onEdited();
                Rebuild(container, editor, onEdited, onMessage);
            };
            buttonRow.Children.Add(quickFill);
        }

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
            addHere.Click += async (_, _) => await AddItem((x, y), editor, border, onEdited, onMessage, rebuild);
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

        border.ContextMenu = BuildContextMenu(border, x, y, item, displayName, shared, editor, rebuild, onEdited, onMessage);

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

    private static ContextMenu BuildContextMenu(Border anchor, int x, int y, ItemDto item, string displayName, SharedItemDataDto? shared, CharacterEditor editor, Action rebuild, Action onEdited, Action<string> onMessage)
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

        // Can't go through Add above — it assumes its action mutates
        // synchronously before onEdited()/rebuild() fire, but this one opens
        // a popup and only mutates once the user actually confirms.
        // Deliberately looser than CanFillStack (no item.Stack < MaxStack
        // clause): reducing a full stack is a supported use, not just
        // topping one up.
        var editAmount = new MenuItem { Header = "Edit amount", IsEnabled = CanEditAmount(item) };
        editAmount.Click += (_, _) => ShowEditAmountFlyout(anchor, x, y, item, shared!, displayName, editor, onEdited, onMessage, rebuild);
        menu.Items.Add(editAmount);

        Add("Set crafter to self", CanSetCrafterToSelf(item, editor), () => { editor.SetItemCrafterAt(x, y); onMessage($"Marked {displayName} as crafted by you"); });
        Add("Clear crafter tag", CanClearCrafter(item), () => { editor.ClearItemCrafterAt(x, y); onMessage($"Cleared crafter tag on {displayName}"); });
        Add("Delete", true, () => { editor.RemoveItemAt(x, y); onMessage($"Deleted {displayName}"); });

        return menu;
    }

    /// <summary>
    /// Opens a small, transient Flyout anchored to the tile's own Border,
    /// pre-filled with the item's current stack — the "Edit amount" menu
    /// entry's target. Built fresh per invocation rather than cached: the
    /// whole tile tree is rebuilt wholesale on every edit anyway (see
    /// <see cref="Rebuild"/>), so there's no stale-Flyout state to manage.
    /// Light-dismiss (click outside) discards with no effect — Avalonia's
    /// own free Flyout behavior, matching the "closing is declining"
    /// convention <see cref="AddItemWindow"/>/<see cref="ConfirmDialog"/>
    /// already use — nothing here mutates unless <c>Commit</c> actually
    /// runs. Enter/Escape need explicit <c>KeyDown</c> handling (Flyout
    /// supplies neither natively), tunnel-routed since NumericUpDown's own
    /// inner TextBox could otherwise consume Enter first.
    /// </summary>
    private static void ShowEditAmountFlyout(
        Border anchor, int x, int y, ItemDto item, SharedItemDataDto shared, string displayName,
        CharacterEditor editor, Action onEdited, Action<string> onMessage, Action rebuild)
    {
        // Maximum is AmountEntry.Ceiling, not shared.MaxStack — see that
        // constant's own doc comment for why a lower, per-item Maximum here
        // reintroduces NumericUpDown's live keystroke-rejection bug. The
        // real bound is still enforced, correctly, once at Commit by
        // CharacterEditor.SetItemStack's own clamp.
        var numeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = AmountEntry.Ceiling,
            Value = Math.Clamp(item.Stack, 1, shared.MaxStack),
            Width = 130,
        };
        var confirm = new Button { Content = "Set" };

        var content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, Margin = new Thickness(10) };
        content.Children.Add(new TextBlock { Text = $"Amount (max {shared.MaxStack}):" });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(numeric);
        row.Children.Add(confirm);
        content.Children.Add(row);

        var flyout = new Flyout { Content = content, Placement = PlacementMode.Bottom };

        void Commit()
        {
            var requested = (int)(numeric.Value ?? item.Stack);
            editor.SetItemStack(x, y, requested);

            // Read back the actual stored value, not the requested one —
            // SetItemStack clamps to the catalog max, so a requested amount
            // above it (allowed to type, per AmountEntry.Ceiling) would
            // otherwise post a status message claiming a value that was
            // never actually set.
            var actual = editor.View.Inventory.Items.Single(i => i.GridX == x && i.GridY == y).Stack;
            onMessage($"Set {displayName}'s amount to {actual}");
            flyout.Hide();
            onEdited();
            rebuild();
        }

        confirm.Click += (_, _) => Commit();

        content.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                flyout.Hide();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        flyout.Opened += (_, _) => numeric.Focus();

        FlyoutBase.SetAttachedFlyout(anchor, flyout);
        FlyoutBase.ShowAttachedFlyout(anchor);
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

        // Read-only — surfacing an already-true fact from the save, not
        // granting one, same category as Unlockables' Trophies field.
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

    private static bool CanFillStackOfType(ItemDto item, IReadOnlySet<ItemType> types) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { MaxStack: > 1 } shared
        && types.Contains(shared.ItemType)
        && item.Stack < shared.MaxStack;

    // Looser than CanFillStack — available even at a full stack, since
    // reducing one is a supported use here.
    private static bool CanEditAmount(ItemDto item) =>
        SharedItemDataCatalog.TryFind(item.PrefabName) is { MaxStack: > 1 };

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
    /// <para>
    /// <paramref name="anchor"/> is the specific empty tile the caller
    /// targeted, when there is one. An empty tile's own "Add item" menu entry
    /// always passes one — that tile is guaranteed to receive a new stack
    /// (see <see cref="CharacterEditor.AddItemsAt"/>). The toolbar button
    /// always passes <c>null</c> — it never targets one tile, so a stackable
    /// item merges/fills wherever it fits (<see cref="CharacterEditor.AddItems"/>);
    /// a non-stackable one falls back to a freshly found empty slot below,
    /// which can come back empty once the toolbar button's own enablement
    /// started allowing a full grid with a stackable-elsewhere item
    /// (<see cref="CanFillStack"/>).
    /// </para>
    /// </summary>
    private static async Task AddItem((int X, int Y)? anchor, CharacterEditor editor, Control sender, Action onEdited, Action<string> onMessage, Action rebuild)
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

        var prefabName = result.Value.PrefabName;
        var shared = SharedItemDataCatalog.TryFind(prefabName);
        var displayName = shared?.DisplayName ?? prefabName;

        if (shared is not { MaxStack: > 1 })
        {
            // Not stackable — the single-unit mechanism AddItemAt has always
            // used, unchanged. Nothing to merge into, so it needs a real
            // empty slot: the anchor tile when the caller targeted one, or a
            // freshly found one otherwise (re-scanned here, not the toolbar
            // button's own pre-picker snapshot, though nothing can have
            // changed underneath a modal picker either way).
            var target = anchor ?? FindFirstEmptySlot(editor.View.Inventory.Items);
            if (target is not { X: var x, Y: var y })
            {
                onMessage($"No room for {displayName}");
                return;
            }

            editor.AddItemAt(x, y, prefabName);
            if (result.Value.SetCrafter)
            {
                editor.SetItemCrafterAt(x, y);
            }

            var qualifier = result.Value.SetCrafter ? " (crafted by you)" : "";
            onMessage($"Added {displayName}{qualifier}");
            onEdited();
            rebuild();
            return;
        }

        // Stackable — merges into existing non-full stacks and/or fills
        // empty slots, splitting the requested amount across as many as it
        // takes. See CharacterEditor.AddItems/AddItemsAt.
        var before = editor.View.Inventory.Items.Where(i => i.PrefabName == prefabName).Sum(i => i.Stack);
        var amount = Math.Max(1, result.Value.Amount);

        if (anchor is { X: var anchorX, Y: var anchorY })
        {
            editor.AddItemsAt(anchorX, anchorY, prefabName, amount, result.Value.SetCrafter);
        }
        else
        {
            editor.AddItems(prefabName, amount, result.Value.SetCrafter);
        }

        // Read back the actual total added, not the requested amount — both
        // a per-item MaxStack clamp and a full grid can cap what actually
        // landed, and only this reflects the truth.
        var after = editor.View.Inventory.Items.Where(i => i.PrefabName == prefabName).Sum(i => i.Stack);
        var actualAdded = after - before;

        var message = actualAdded switch
        {
            0 => $"No room for {displayName}",
            _ when actualAdded < amount => $"Added {displayName} (amount {actualAdded} of {amount} — inventory full)",
            _ => BuildAddedMessage(displayName, result.Value.SetCrafter, actualAdded),
        };

        onMessage(message);
        onEdited();
        rebuild();
    }

    private static string BuildAddedMessage(string displayName, bool setCrafter, int actualAdded)
    {
        var qualifiers = new List<string>();
        if (setCrafter)
        {
            qualifiers.Add("crafted by you");
        }

        if (actualAdded > 1)
        {
            qualifiers.Add($"amount {actualAdded}");
        }

        var suffix = qualifiers.Count > 0 ? $" ({string.Join(", ", qualifiers)})" : "";
        return $"Added {displayName}{suffix}";
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
