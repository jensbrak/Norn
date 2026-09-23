using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Click-to-carry state and rendering for the Inventory tab's drag-and-drop:
/// pick up a slot's contents with one click, place with a second — not a
/// held mouse-drag, matching Valheim's own mechanic (<c>InventoryGui</c>'s
/// <c>SetupDragItem</c>/<c>OnSelectedItem</c> never mutate on pickup either;
/// all real data movement happens at placement time, via
/// <see cref="CharacterEditor.MoveItemAt"/>). Kept out of
/// <see cref="InventoryTabModule"/> entirely — same "concentrate physically"
/// precedent as <c>TabRows.cs</c>/<c>IconButtons.cs</c>/<c>AppearanceColors.cs</c>
/// — so the tab module's own tile-building code doesn't grow a state machine
/// alongside it.
/// <para>
/// One instance per <see cref="InventoryTabModule.Build"/> call, threaded
/// through <c>Rebuild</c>/<c>BuildGrid</c>/<c>BuildTile</c> the same way
/// <c>CharacterEditor</c>/<c>onEdited</c>/<c>onMessage</c> already are — it
/// must outlive a single rebuild (an overflow merge keeps carrying the
/// leftover across one), but not a fresh <c>Build</c> call (a new character
/// load starts clean).
/// </para>
/// </summary>
internal sealed class InventoryCarry
{
    private readonly record struct Held(
        int X, int Y, int Amount, int OriginalTotal, string PrefabName, string DisplayName, bool Stackable,
        bool IsSplit, long CrafterId, bool Cheated);

    private Held? _held;
    private Border? _sourceTile;
    private Border? _hoveredTile;
    private bool _justCancelled;
    private readonly List<(Border Tile, ContextMenu? Menu)> _tiles = [];
    private readonly TextBlock _labelText = new() { Foreground = Brushes.White, FontSize = 12 };
    private readonly Border _label;
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };

    public InventoryCarry()
    {
        _label = new Border
        {
            Background = Brushes.Black,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 3),
            Child = _labelText,
            IsVisible = false,
            IsHitTestVisible = false,
        };
        _overlay.Children.Add(_label);
    }

    public bool IsCarrying => _held is not null;

    /// <summary>
    /// Wraps <paramref name="content"/> with the floating cursor label's host
    /// layer and the Escape-to-cancel key handler — call once per
    /// <see cref="InventoryTabModule.Build"/>. A plain <see cref="Grid"/>
    /// overlay (both children share the one cell) rather than a
    /// <see cref="Popup"/>: Avalonia's <c>Popup</c> doesn't live-track the
    /// pointer once open, and re-opening it every <c>PointerMoved</c> would
    /// flicker — an ordinary <see cref="Canvas"/>-positioned control
    /// repositioned on every move is simpler and doesn't fight the layout
    /// system.
    /// </summary>
    public Control WithOverlay(Control content)
    {
        var host = new Grid();
        host.Children.Add(content);
        host.Children.Add(_overlay);

        host.PointerMoved += (_, e) =>
        {
            if (_held is null)
            {
                return;
            }

            var pos = e.GetPosition(_overlay);
            Canvas.SetLeft(_label, pos.X + 14);
            Canvas.SetTop(_label, pos.Y + 14);
        };

        // Attached to the TopLevel, not to `host`: a Tunnel-routed key
        // handler only reaches a control that's an ancestor of whatever
        // currently has keyboard focus, and nothing in this tab ever grabs
        // focus (the tiles are plain, non-focusable Borders). The TopLevel
        // is the root of every tunnel route regardless of what, if
        // anything, is focused. Captured (not re-resolved) at attach time
        // and removed symmetrically on detach — `host` may already be
        // unparented by then, and Build/WithOverlay reruns on every
        // character load, so an unremoved handler would leak one per load
        // onto the shared window.
        TopLevel? topLevel = null;

        void OnEscape(object? _, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsCarrying)
            {
                Cancel();
                e.Handled = true;
            }
        }

        host.AttachedToVisualTree += (_, _) =>
        {
            topLevel = TopLevel.GetTopLevel(host);
            topLevel?.AddHandler(InputElement.KeyDownEvent, OnEscape, RoutingStrategies.Tunnel);
        };
        host.DetachedFromVisualTree += (_, _) =>
        {
            topLevel?.RemoveHandler(InputElement.KeyDownEvent, OnEscape);
            topLevel = null;
        };

        return host;
    }

    /// <summary>Sets or clears a tile's hover-highlight border — a plain,
    /// single-state cue (no "invalid target" state exists, since every
    /// placement always resolves to a move/swap/merge/merge-with-remainder).
    /// Also suppresses that tile's own <c>ToolTip</c> for the duration of
    /// this hover while carrying — the item-facts tooltip otherwise
    /// visually and "mentally" competes with the carry label, weakening the
    /// sense that a drag is in progress. Re-enabled on every hover event
    /// regardless of direction, so
    /// a tile's tooltip service self-corrects the next time it's entered or
    /// exited without needing a dedicated "carry just ended" signal —
    /// placements already rebuild the grid (fresh <c>Border</c>s, tooltip
    /// service enabled by default), and a cancel self-corrects on the next
    /// <c>PointerExited</c>. Called from a tile's own
    /// <c>PointerEntered</c>/<c>PointerExited</c> handlers.
    /// <para>
    /// An ordinary hover-exit never clears <see cref="_sourceTile"/>'s own
    /// persistent highlight (see <see cref="ApplySourceHighlight"/>) — only
    /// <see cref="Cancel"/> and a fresh rebuild (which simply doesn't
    /// re-apply it once nothing's held) do that.
    /// </para>
    /// </summary>
    public void SetHover(Border tile, bool entering)
    {
        ToolTip.SetServiceEnabled(tile, !IsCarrying);

        if (entering && IsCarrying)
        {
            _hoveredTile = tile;
            tile.BorderBrush = Brushes.Gold;
            tile.BorderThickness = new Thickness(3);
            return;
        }

        if (ReferenceEquals(_hoveredTile, tile))
        {
            _hoveredTile = null;
        }

        if (tile != _sourceTile)
        {
            tile.BorderBrush = null;
            tile.BorderThickness = new Thickness(0);
        }
    }

    /// <summary>
    /// Applies (or leaves untouched) the persistent source-tile highlight —
    /// same gold treatment as the hovered-target cue, kept lit for the
    /// whole carry so it stays visibly connected to where it started, not
    /// just to whatever's currently under the pointer.
    /// Call once per tile at build/rebuild time (both the empty- and
    /// occupied-tile branches of <c>InventoryTabModule.BuildTile</c>) — a
    /// no-op for every tile except whichever one is currently the carry's
    /// source, which is how this stays correct across a rebuild that
    /// recreates every tile's <see cref="Border"/> (an overflow merge keeps
    /// carrying with the same source position, but the old <c>Border</c>
    /// instance is gone).
    /// </summary>
    public void ApplySourceHighlight(Border tile, int x, int y)
    {
        if (_held is not { } held || held.X != x || held.Y != y)
        {
            return;
        }

        _sourceTile = tile;
        tile.BorderBrush = Brushes.Gold;
        tile.BorderThickness = new Thickness(3);
    }

    /// <summary>Clears the tile registry before a fresh build pass — call
    /// once per <c>BuildGrid</c>, before any <see cref="RegisterTile"/>
    /// calls, so the registry never accumulates stale <see cref="Border"/>s
    /// from a previous rebuild.</summary>
    public void ResetTiles() => _tiles.Clear();

    /// <summary>
    /// Registers a tile's real <see cref="ContextMenu"/> (<c>null</c> for a
    /// tile with none): nulls it out on the tile for the duration of a
    /// carry (also covers the keyboard "open context menu" shortcut, which
    /// goes through the same path as a right-click), and — the mechanism
    /// that actually matters — subscribes to the menu's own
    /// <see cref="ContextMenu.Opening"/> event and cancels it whenever
    /// <see cref="_justCancelled"/> is set. <c>Opening</c> is what
    /// <c>Control</c>'s internal right-click handling checks
    /// (<c>CancelOpening()</c>) before ever calling <c>Open()</c>, so
    /// cancelling here stops the menu before it becomes visible regardless
    /// of event routing or handler order.
    /// </summary>
    public void RegisterTile(Border tile, ContextMenu? menu)
    {
        _tiles.Add((tile, menu));
        tile.ContextMenu = IsCarrying ? null : menu;

        if (menu is not null)
        {
            // Self-consuming rather than time-boxed: this is the one event
            // guaranteed to pair with the exact right-click Cancel() needs
            // to cover, so resetting the flag here — instead of on a timer —
            // needs no assumption about how much time separates the press
            // that cancels the carry from the release that raises this.
            menu.Opening += (_, e) =>
            {
                if (_justCancelled)
                {
                    e.Cancel = true;
                }

                _justCancelled = false;
            };
        }
    }

    private void SuppressContextMenus(bool suppress)
    {
        foreach (var (tile, menu) in _tiles)
        {
            tile.ContextMenu = suppress ? null : menu;
        }
    }

    /// <summary>Cancels a carry from a right-click, if one is active —
    /// context-menu suppression for the same click is handled separately,
    /// see <see cref="RegisterTile"/>. Called from both the tile's
    /// right-button <c>PointerPressed</c> branch and its
    /// <c>ContextRequested</c> event in
    /// <c>InventoryTabModule.WireCarryInput</c>, kept as two call sites
    /// since either is a reasonable place for a right-click gesture to
    /// surface depending on input method. Returns whether a carry was
    /// actually cancelled.</summary>
    public bool CancelIfCarrying()
    {
        if (!IsCarrying)
        {
            return false;
        }

        Cancel();
        return true;
    }

    public void Cancel()
    {
        _held = null;
        _label.IsVisible = false;

        // Clears both the source tile's persistent highlight and whichever
        // tile the pointer currently sits over — the latter is almost
        // always a different tile once you've moved toward a target, and
        // would otherwise only clear on the next PointerExited.
        ClearBorder(_sourceTile);
        ClearBorder(_hoveredTile);
        _sourceTile = null;
        _hoveredTile = null;

        // Deferred: this can run inside the same PointerPressed handler
        // that a right-click cancel fires from, before that click's own
        // menu-open decision has resolved. Restoring the real menus here is
        // tidiness, not correctness — RegisterTile's Opening subscription
        // is what actually stops the menu.
        Dispatcher.UIThread.Post(() => SuppressContextMenus(false));

        // Set for RegisterTile's Opening handler to consume — see there for
        // why this needs to be a flag rather than just re-checking
        // IsCarrying (already false again by the time Opening fires).
        _justCancelled = true;
    }

    private static void ClearBorder(Border? tile)
    {
        if (tile is not null)
        {
            tile.BorderBrush = null;
            tile.BorderThickness = new Thickness(0);
        }
    }

    /// <summary>
    /// Plain-left-click entry point. Picks up <paramref name="item"/> (whole
    /// stack) if nothing's currently held; otherwise attempts to place the
    /// held item onto <paramref name="x"/>/<paramref name="y"/> via
    /// <see cref="CharacterEditor.MoveItemAt"/> and reports what happened.
    /// Clicking the carried item's own source tile again cancels — matches
    /// the real game's "drop onto its own slot" no-op, handled here rather
    /// than inside the mutator so it reads as an explicit cancel rather than
    /// a silent no-op the caller has to infer.
    /// </summary>
    public void HandleClick(
        Border tile, int x, int y, ItemDto? item, SharedItemDataDto? shared, string displayName,
        CharacterEditor editor, Action onEdited, Action<string> onMessage, Action rebuild)
    {
        if (_held is not { } held)
        {
            if (item is null)
            {
                return;
            }

            Pick(tile, x, y, item.Stack, isSplit: false, item, shared, displayName);
            return;
        }

        if (x == held.X && y == held.Y)
        {
            Cancel();
            return;
        }

        var targetBefore = editor.View.Inventory.Items.FirstOrDefault(i => i.GridX == x && i.GridY == y);
        var placed = editor.MoveItemAt(held.X, held.Y, held.Amount, x, y);
        if (placed == 0)
        {
            // A dead no-op — most reachably, a split carry (which can never
            // swap, only move-to-empty or merge) dropped onto an occupied,
            // different-type slot; also the rare worldLevel-mismatch case.
            // The real game leaves this completely silent, which reads as
            // broken rather than rejected with nothing on screen to say the
            // drop didn't land — the message below is Norn's own addition;
            // the carry itself still stays active either way, matching the
            // game.
            if (targetBefore is not null)
            {
                onMessage($"Can't place {held.DisplayName} on {ResolveDisplayName(targetBefore.PrefabName)} — different item");
            }

            return;
        }

        onMessage(BuildMessage(held, targetBefore, editor, x, y, placed));
        onEdited();
        rebuild();

        if (placed >= held.Amount)
        {
            Cancel();
        }
        else
        {
            _held = held with { Amount = held.Amount - placed };
            _labelText.Text = FormatLabel(_held.Value);
        }
    }

    /// <summary>Shift-click entry point: opens a slider popup for splitting
    /// off part of a stack, matching Valheim's own split dialog (default
    /// position rounds the held portion UP —
    /// <c>Mathf.CeilToInt(stack / 2f)</c>). Confirming only starts carrying
    /// the chosen amount; no data changes until it's actually placed, same
    /// as the real game's own split (the source stays full until then).
    /// </summary>
    public void BeginSplit(
        Border anchor, int x, int y, ItemDto item, SharedItemDataDto shared, string displayName)
    {
        var slider = new Slider { Minimum = 1, Maximum = item.Stack, Value = Math.Ceiling(item.Stack / 2.0) };
        var readout = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                readout.Text = $"{(int)slider.Value}";
            }
        };
        readout.Text = $"{(int)slider.Value}";

        var confirm = new Button { Content = "Split" };
        var content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, Margin = new Thickness(10) };
        content.Children.Add(new TextBlock { Text = $"Split off how many (of {item.Stack}):" });
        content.Children.Add(readout);
        content.Children.Add(slider);
        content.Children.Add(confirm);

        var flyout = new Flyout { Content = content, Placement = PlacementMode.Bottom };

        confirm.Click += (_, _) =>
        {
            Pick(anchor, x, y, (int)slider.Value, isSplit: true, item, shared, displayName);
            flyout.Hide();
        };

        FlyoutBase.SetAttachedFlyout(anchor, flyout);
        FlyoutBase.ShowAttachedFlyout(anchor);
    }

    /// <summary>
    /// Starts carrying. Also proactively closes/suppresses <paramref
    /// name="tile"/>'s own <c>ToolTip</c> immediately — <see cref="SetHover"/>
    /// alone only catches the *next* hover, so a tile the pointer was
    /// already sitting on when the pickup click landed would otherwise keep
    /// showing (or start showing) its tooltip until the pointer moves.
    /// </summary>
    private void Pick(Border tile, int x, int y, int amount, bool isSplit, ItemDto item, SharedItemDataDto? shared, string displayName)
    {
        var stackable = shared is { MaxStack: > 1 };
        _held = new Held(x, y, amount, item.Stack, item.PrefabName, displayName, stackable, isSplit, item.CrafterId, item.Cheated);
        _labelText.Text = FormatLabel(_held.Value);
        _label.IsVisible = true;

        // Applied immediately rather than waiting for ApplySourceHighlight's
        // next build pass — a pickup deliberately doesn't rebuild the grid
        // (nothing was mutated), so nothing would otherwise re-run it.
        _sourceTile = tile;
        tile.BorderBrush = Brushes.Gold;
        tile.BorderThickness = new Thickness(3);

        ToolTip.SetIsOpen(tile, false);
        ToolTip.SetServiceEnabled(tile, false);
        SuppressContextMenus(true);
    }

    /// <summary>
    /// The floating cursor label's text — prefixed with an explicit verb
    /// (an unprefixed item name alone reads as too weak a cue that a carry
    /// is actually in progress, especially next to the tile's own
    /// tooltip) and, for a stackable item, the
    /// amount actually being carried against the stack it came from — not
    /// just the current tile's own total, since a carry can outlive the tile
    /// it started on (an overflow merge shrinks the held amount but keeps
    /// carrying). "Splitting" only when the carry started from the split
    /// popup, even if a later overflow shrinks it further — the verb
    /// describes how this carry began, not the current amount.
    /// </summary>
    private static string FormatLabel(Held held)
    {
        var verb = held.IsSplit ? "Splitting" : "Moving";
        var suffix = held.Stackable ? $" ({held.Amount} of {held.OriginalTotal})" : "";
        return $"{verb}: {held.DisplayName}{suffix}";
    }

    /// <summary>
    /// Describes what a placement actually did, for the status bar. A move
    /// or swap gets a plain confirmation — nothing was discarded, so there's
    /// nothing more to say. A merge is where Norn adds what the real game
    /// doesn't: a note when the carried item's crafter tag or cheated flag
    /// didn't carry over, since a merge only ever keeps the *target*
    /// stack's own values (<see cref="CharacterEditor.MoveItemAt"/>'s own
    /// doc comment). Swap vs. merge is told apart by simply re-reading the
    /// source position after the mutation — a swap leaves a different prefab
    /// there, a merge leaves it empty or still holding the same prefab's
    /// remainder — rather than re-deriving the eligibility rule a second
    /// time here, which would risk silently drifting from
    /// <see cref="CharacterEditor.MoveItemAt"/>'s own copy of it.
    /// </summary>
    private static string BuildMessage(Held held, ItemDto? targetBefore, CharacterEditor editor, int x, int y, int placed)
    {
        if (targetBefore is null)
        {
            return $"Moved {held.DisplayName}";
        }

        var targetDisplayName = ResolveDisplayName(targetBefore.PrefabName);
        var stillAtSource = editor.View.Inventory.Items.FirstOrDefault(i => i.GridX == held.X && i.GridY == held.Y);
        var wasSwap = stillAtSource is not null && stillAtSource.PrefabName != held.PrefabName;

        if (wasSwap)
        {
            return $"Swapped {held.DisplayName} with {targetDisplayName}";
        }

        var message = $"Merged into {targetDisplayName}";

        if (held.CrafterId != 0 && held.CrafterId != targetBefore.CrafterId)
        {
            message += " (crafter tag not carried over)";
        }

        if (held.Cheated && !targetBefore.Cheated)
        {
            message += " (cheated flag not carried over)";
        }

        if (placed < held.Amount)
        {
            message += $" — {placed} of {held.Amount}, {held.Amount - placed} still held";
        }

        return message;
    }

    private static string ResolveDisplayName(string prefabName) =>
        SharedItemDataCatalog.TryFind(prefabName)?.DisplayName ?? prefabName;
}
