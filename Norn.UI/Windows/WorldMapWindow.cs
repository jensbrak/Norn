using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// A single world's decoded map, shown full-window. Modal over
/// <paramref name="owner"/> (<see cref="Open"/>) — deliberately, unlike an
/// earlier draft of this window: nothing stops a user from clicking "View
/// map" again, for the same world or a different one, while one is already
/// open, and nothing in this window's own state expects more than one
/// instance to exist at a time. Blocking the owner is the cheap fix; a
/// multi-window-aware version (dedupe by world ID, let genuinely different
/// worlds coexist) isn't worth building for a POC-tier viewer.
/// <para>
/// The pin count sits in the title bar, not the rendered map itself —
/// deliberately not the pins, and deliberately not rendered at all
/// (<see cref="WorldMapDto"/>'s own doc explains why: clutter at the
/// thousands-of-pins scale a well-played world can reach, and the "part of
/// gameplay, not editor scope" line this project already draws elsewhere).
/// A bare count gives the same "which world is this, without naming it"
/// identity signal the exploration shape itself already provides, at zero
/// visual cost. And deliberately not on the Worlds tab row either — it's
/// only available once the blob is actually decoded (this window is what
/// triggers that), so it belongs to what this window reveals, not to a
/// row that exists whether or not the map's ever been viewed.
/// </para>
/// <para>
/// The title's own/received split tracks the same checkbox that drives the
/// render — both partitions ultimately answer "mine vs. someone else's",
/// even though they rest on different underlying storage
/// (<see cref="WorldMapDto"/>'s <c>Explored</c>/<c>ExploredOthers</c> are
/// two real arrays; <c>OwnPinCount</c>/<c>ReceivedPinCount</c> are a
/// snapshot split on one mutable field, <c>ownerID</c>: "own" here means
/// "not currently attributed to anyone else", not "authored by me").
/// </para>
/// <para>
/// The four per-world points get their
/// own independent toggle ("Show points") and a small overlay
/// (<see cref="WorldMapMarkers"/>), unlike pins: at most four per world,
/// not the thousands a well-played world's pins can reach, so the clutter
/// argument that kept pins off the render entirely doesn't apply here —
/// default is checked, not unchecked. A legend accompanies the toggle since
/// the shapes (triangle/circle/square/diamond) aren't self-explanatory on
/// first look. The checkbox itself sits in the same row as "Show explored
/// by others", right-aligned — the two used to be one row apart (the
/// legend's own earlier position), which read as visually disconnected.
/// The legend lives inside <c>mapArea</c> instead, bottom-right (longest
/// label bottommost, where the disc's own curve leaves the most spare
/// corner space): unlike the always-present checkbox, the legend's own
/// visibility toggles with the checkbox, and an earlier version that docked
/// it above the map (rather than overlaying it inside the same fixed-size
/// area the disc renders into) visibly resized the disc on every toggle.
/// </para>
/// <para>
/// No longer view-only: this window now owns three mutating actions, in a
/// row below the toggles, left-to-right in "least destructive/most
/// reversible-feeling first" order — the same ordering convention
/// <c>WorldsTabModule</c>'s own header buttons already use:
/// <list type="number">
/// <item>"Reveal world" (<see cref="CharacterEditor.ExploreAllMap"/>) — adds
/// information, destroys nothing. Moved here from <c>WorldsTabModule</c>'s
/// section-header button (where it was labeled "Explore All"), alongside its
/// own dismissible spoiler warning (<see cref="ExploreAllLegendExplanation"/>/
/// <see cref="Settings.ShowExploreWorldWarning"/>, same flag as before, just
/// a new home) — same "next to the view that shows what it'd affect"
/// reasoning as the two clears below.</item>
/// <item>"Clear received map data" (<see cref="CharacterEditor.ClearReceivedMapData"/>)
/// — removes only what was received via a cartography table, leaves this
/// character's own exploration/pins untouched.</item>
/// <item>"Clear map data" (<see cref="CharacterEditor.ClearWorldMapData"/>) —
/// wipes the blob entirely, own data included. Moved here from
/// <c>WorldsTabModule</c>'s own per-row button.</item>
/// </list>
/// Deliberately placed here rather than left on the Worlds tab: deciding
/// whether to act on any of the three benefits from actually looking at the
/// map first, which this window is the only place that does. "Reveal world"
/// and "Clear received map data" re-decode and re-render in place after
/// acting, since the rest of the map survives either one; "Clear map data"
/// wipes the blob entirely, so there is nothing left to show afterward and
/// the window closes itself. "Clear received map data" additionally only
/// enables when there's something to clear (<see cref="HasReceivedMapData"/>)
/// — same condition the "Show explored by others" checkbox's own
/// <see cref="CheckBox.IsEnabled"/> now uses, so a world with nothing
/// received grays out both controls together rather than leaving one
/// active with nothing for it to affect. Every action calls <c>onEdited</c>
/// immediately, same as every other mutator in the app; <c>WorldsTabModule.ShowMap</c>
/// rebuilds its own row list once this window closes, regardless of which
/// action (if any) fired, since any of the three can change what that row
/// shows ("Has map data", button enablement).
/// </para>
/// </summary>
internal sealed class WorldMapWindow : Window
{
    // note: NOT IN SOURCE — presentation-only backdrop, not
    // sampled from the game. WorldMapRenderer's bitmap is now a disc with a
    // transparent margin outside the world edge (MinimapGeometry); without a
    // dark backdrop behind it, that margin would just show through to
    // whatever's behind this window instead of reading as "space."
    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromRgb(10, 10, 15));

    /// <summary>Moved here from <c>WorldsTabModule</c> (which had it as
    /// <c>ExploreAllLegendExplanation</c>, text unchanged) alongside "Reveal
    /// world" itself — a gameplay spoiler warning, not a data-loss one:
    /// filling in a world's entire map at once, the same effect as the
    /// game's own (cheat-only, host-only) <c>exploremap</c> console command.
    /// Still gated on <see cref="Settings.ShowExploreWorldWarning"/>, the
    /// same flag as before the move, so a dismissal from either home
    /// persists identically.</summary>
    private const string ExploreAllLegendExplanation =
        "\"Reveal world\" fills in a world's entire map at once — the same effect as the game's own "
        + "(cheat-only) \"exploremap\" command. It only fills in your own exploration, not map data "
        + "shared with you via a cartography table, and only affects the current character.";

    // Shortest label first: the legend block anchors to the map area's
    // bottom-right corner and lays out top-to-bottom, so the first entry
    // here ends up topmost (least spare width, since the disc's curve sits
    // closest to the edge there) and the last ends up flush against the
    // bottom (most spare width).
    private static readonly WorldMapMarkerKind[] LegendOrder =
    [
        WorldMapMarkerKind.Home,
        WorldMapMarkerKind.Spawn,
        WorldMapMarkerKind.Death,
        WorldMapMarkerKind.Logout,
    ];

    private readonly CharacterEditor _editor;
    private readonly long _worldId;
    private readonly Action _onEdited;
    private readonly Action<string> _onMessage;
    private WorldMapDto _map;
    private readonly IReadOnlyList<(WorldMapMarkerKind Kind, PositionDto Position)> _points;
    private readonly Panel _mapArea;
    private readonly Image _image;
    private readonly Canvas _markers;
    private readonly Control _legend;
    private readonly CheckBox _includeOthers;
    private readonly Button _clearReceivedButton;
    private bool _showPoints = AppStateStore.Current.WorldMapShowPoints;

    /// <summary>Whether there is anything for "Clear received map data" to
    /// do — gates that button's <see cref="Button.IsEnabled"/> the same way
    /// <c>InventoryTabModule</c>'s <c>CanRepair</c>/<c>CanFillStack</c> gate
    /// theirs, rather than performing a silent no-op encode (which would
    /// still incur the R1-&gt;R2 downgrade <see cref="Minimap.Encode"/>'s own
    /// doc comment warns about, for a world that had nothing to clear in the
    /// first place — the common case for most worlds, unlike
    /// <see cref="CharacterEditor.ExploreAllMap"/>, which has no equivalent
    /// guard).</summary>
    private bool HasReceivedMapData => _map.ReceivedPinCount > 0 || Array.Exists(_map.ExploredOthers, b => b != 0);

    private WorldMapWindow(CharacterEditor editor, WorldDto world, WorldMapDto map, Action onEdited, Action<string> onMessage)
    {
        _editor = editor;
        _worldId = world.WorldId;
        _onEdited = onEdited;
        _onMessage = onMessage;
        _map = map;
        _points = BuildPoints(world);

        Icon = AppIcon.Default;
        Width = 640;
        Height = 680;
        Background = Backdrop;

        _legend = BuildLegend();
        _legend.HorizontalAlignment = HorizontalAlignment.Right;
        _legend.VerticalAlignment = VerticalAlignment.Bottom;
        _legend.Margin = new Avalonia.Thickness(8);
        _legend.IsVisible = _showPoints;

        _includeOthers = new CheckBox
        {
            Content = "Show explored by others",
            IsChecked = AppStateStore.Current.WorldMapShowExploredByOthers,
            IsEnabled = HasReceivedMapData,
            Margin = new Avalonia.Thickness(8),
        };
        _includeOthers.IsCheckedChanged += (_, _) =>
        {
            AppStateStore.Current.WorldMapShowExploredByOthers = _includeOthers.IsChecked == true;
            AppStateStore.Save();
            UpdateRender(_includeOthers.IsChecked == true);
        };

        var showPoints = new CheckBox
        {
            Content = "Show points",
            IsChecked = _showPoints,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(8),
        };
        showPoints.IsCheckedChanged += (_, _) =>
        {
            _showPoints = showPoints.IsChecked == true;
            AppStateStore.Current.WorldMapShowPoints = _showPoints;
            AppStateStore.Save();
            _legend.IsVisible = _showPoints;
            RepositionMarkers();
        };

        _image = new Image { Stretch = Stretch.Uniform };

        _markers = new Canvas { IsHitTestVisible = false };

        // Only the legend lives inside mapArea (bottom-right overlay) —
        // its visibility is what toggles, so it's the one that must never
        // change how much room the disc itself gets to render into (see
        // this class's own doc comment). The checkbox itself is always
        // present regardless of state, so docking it in the row above
        // "Show explored by others" — instead of overlaying it inside
        // mapArea — doesn't reintroduce that problem, and reads far better
        // than two checkboxes one row apart, left and right of each other.
        _mapArea = new Panel();
        _mapArea.Children.Add(_image);
        _mapArea.Children.Add(_markers);
        _mapArea.Children.Add(_legend);
        _mapArea.SizeChanged += (_, _) => RepositionMarkers();

        var toggleRow = new DockPanel();
        DockPanel.SetDock(_includeOthers, Dock.Left);
        toggleRow.Children.Add(_includeOthers);
        toggleRow.Children.Add(showPoints); // fills the remainder, right-aligned by its own HorizontalAlignment

        // Left-to-right "least destructive/most reversible-feeling first"
        // ordering, matching WorldsTabModule's own header-button pair
        // (formerly Explore All / Remove world, now just Remove world) —
        // revealing adds information without destroying anything, clearing
        // received data only touches what wasn't this character's own, and
        // clearing everything is the wholesale action, over on the right.
        var revealButton = new Button { Content = "Reveal world" };
        ToolTip.SetTip(revealButton,
            "Reveals this world's entire map (your own exploration only — doesn't affect map data shared with you).");
        revealButton.Click += (_, _) =>
        {
            _editor.ExploreAllMap(_worldId);
            _onMessage($"World {_worldId} fully explored");
            _onEdited();

            // Same "still has map data, refresh in place" shape as "Clear
            // received map data" below — the world's own exploration only
            // grows, nothing here can make HasReceivedMapData go stale.
            _map = _editor.DecodeWorldMap(_worldId) ?? _map;
            UpdateRender(_includeOthers.IsChecked == true);
        };

        _clearReceivedButton = new Button { Content = "Clear received map data" };
        ToolTip.SetTip(_clearReceivedButton,
            "Clears exploration and pins received from others via a cartography table. "
            + "Leaves your own exploration and pins untouched.");
        _clearReceivedButton.IsEnabled = HasReceivedMapData;
        _clearReceivedButton.Click += (_, _) =>
        {
            _editor.ClearReceivedMapData(_worldId);
            _onMessage($"Received map data cleared for world {_worldId}");
            _onEdited();

            // Unlike ClearWorldMapData below, the world still has map data
            // afterward — re-decode and re-render in place rather than
            // closing, so the "look, then decide" flow this feature exists
            // for actually shows the result. HasReceivedMapData goes false
            // right after this, so both controls it gates go stale together.
            _map = _editor.DecodeWorldMap(_worldId) ?? _map;
            _clearReceivedButton.IsEnabled = HasReceivedMapData;
            _includeOthers.IsEnabled = HasReceivedMapData;
            UpdateRender(_includeOthers.IsChecked == true);
        };

        var clearAllButton = new Button { Content = "Clear map data" };
        ToolTip.SetTip(clearAllButton,
            "Clears ALL cached map data for this world — your own exploration and pins too, "
            + "not just what was received. Closes this window, since there's nothing left to view.");
        clearAllButton.Click += (_, _) =>
        {
            _editor.ClearWorldMapData(_worldId);
            _onMessage($"Map data cleared for world {_worldId} (exploration and pins both removed)");
            _onEdited();
            Close();
        };

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Avalonia.Thickness(8, 0) };
        actionRow.Children.Add(revealButton);
        actionRow.Children.Add(_clearReceivedButton);
        actionRow.Children.Add(clearAllButton);

        var panel = new DockPanel();

        if (SettingsStore.Current.ShowExploreWorldWarning)
        {
            var legend = TabRows.BuildDismissibleLegend(
                ExploreAllLegendExplanation,
                () => SettingsStore.Current.ShowExploreWorldWarning = false);
            DockPanel.SetDock(legend, Dock.Top);
            panel.Children.Add(legend);
        }

        DockPanel.SetDock(toggleRow, Dock.Top);
        DockPanel.SetDock(actionRow, Dock.Top);
        panel.Children.Add(toggleRow);
        panel.Children.Add(actionRow);
        panel.Children.Add(_mapArea);

        Content = DialogChrome.Wrap(panel);

        UpdateRender(_includeOthers.IsChecked == true);
    }

    private void UpdateRender(bool includeOthers)
    {
        // Each render allocates a fresh WriteableBitmap — a 2048² BGRA image
        // is ~16 MiB of unmanaged pixel storage — and assigning Source used
        // to just drop the previous one on the floor, leaving reclamation to
        // whenever finalization got around to it. Toggling the exploration
        // checkbox repeatedly, or reopening the window, stacked those up
        // (found in review). Dispose the outgoing bitmap explicitly, and the
        // last one on close (see OnClosed).
        var previous = _image.Source as IDisposable;
        _image.Source = WorldMapRenderer.Render(_map, includeOthers);
        previous?.Dispose();

        Title = includeOthers
            ? $"World {_worldId} — Map ({TabRows.Pluralize(_map.OwnPinCount, "own pin")}, {TabRows.Pluralize(_map.ReceivedPinCount, "received pin")})"
            : $"World {_worldId} — Map ({TabRows.Pluralize(_map.OwnPinCount, "pin")})";

        RepositionMarkers();
    }

    /// <summary>Releases the last rendered bitmap when the window closes —
    /// the counterpart to <see cref="UpdateRender"/>'s disposal of each
    /// superseded one (found in review).</summary>
    protected override void OnClosed(EventArgs e)
    {
        var last = _image.Source as IDisposable;
        _image.Source = null;
        last?.Dispose();

        base.OnClosed(e);
    }

    /// <summary>
    /// Converts each point's world coordinate to a screen position and
    /// redraws the marker overlay — called on every render update, points
    /// toggle, and <see cref="_mapArea"/> resize, since <see cref="Image"/>'s
    /// <c>Stretch.Uniform</c> letterboxes the bitmap inside the container and
    /// that scale/offset changes with window size.
    /// </summary>
    /// <remarks>
    /// BUG FIX. Markers drifted away from the disc on resize, badly, but
    /// only once the window's aspect ratio diverged noticeably from square —
    /// a non-square resize (e.g. very wide/short) reproduced it on the very
    /// first resize, no repeated resizing needed.
    /// <para>
    /// The letterbox math here previously read <c>_image.Bounds</c> for the
    /// container size, which is wrong for an <see cref="Image"/> using
    /// <c>Stretch.Uniform</c>: that control's own arranged box is ALREADY
    /// the letterboxed square Avalonia computed for it (its Measure pass
    /// returns a Uniform-constrained desired size from the 2048×2048 source,
    /// and Arrange then centers that box within whatever space its parent
    /// actually handed it — it does not stretch to fill that space, despite
    /// having no explicit alignment override). Reading its Bounds and then
    /// running this method's OWN separate letterbox calculation on top of
    /// that already-letterboxed box double-applies the transform: correct
    /// when the container is roughly square (the two boxes coincide, so the
    /// bug was invisible), wildly wrong the more the container's aspect
    /// ratio diverges from 1:1, since Image's self-constrained box shrinks
    /// away from the container on the axis with the smaller scale factor
    /// while staying centered within it.
    /// </para>
    /// <para>
    /// <see cref="Canvas"/> has no such self-constraint — no intrinsic
    /// content to measure, so it always fills whatever rect it's arranged
    /// into — which is why <see cref="_markers"/> itself was never
    /// misplaced, only the markers positioned inside it by hand. The fix is
    /// to use <see cref="_mapArea"/>'s own bounds (the real container both
    /// <see cref="_image"/> and <see cref="_markers"/> are children of, and
    /// what Avalonia's own internal Stretch.Uniform math is actually
    /// letterboxing the bitmap against) instead of asking the letterboxed
    /// child for a size that was never the container's in the first place.
    /// </para>
    /// </remarks>
    private void RepositionMarkers()
    {
        _markers.Children.Clear();

        if (!_showPoints || _points.Count == 0)
        {
            return;
        }

        var textureSize = _map.TextureSize;
        var controlWidth = _mapArea.Bounds.Width;
        var controlHeight = _mapArea.Bounds.Height;
        if (controlWidth <= 0 || controlHeight <= 0 || textureSize <= 0)
        {
            return;
        }

        var scale = Math.Min(controlWidth / textureSize, controlHeight / textureSize);
        var offsetX = (controlWidth - textureSize * scale) / 2;
        var offsetY = (controlHeight - textureSize * scale) / 2;

        // Solid markers (spawn/logout) drawn first, hollow ones (home/death)
        // last/on top — deliberately, not just because BuildPoints happens
        // to end with the two hollow kinds. A hollow shape has no fill, so
        // stacking it over a solid one still leaves the solid one's fill
        // showing through its outline's own interior, while the outline
        // ring itself stays visible around it — the reverse order would
        // fully hide the hollow shape under the solid one's fill. Real
        // motivation, not theoretical: corpus analysis found spawn and home
        // sit at the exact same
        // position in 71% of worlds with a claimed bed — a direct
        // consequence of the game's own mechanics, not a rare fluke — while
        // every other pair never coincided once across 101 real world
        // entries. A stable sort, not a rewrite of _points' own order,
        // which BuildPoints still controls for unrelated reasons.
        foreach (var (kind, position) in _points.OrderBy(p => WorldMapMarkers.IsSolid(p.Kind) ? 0 : 1))
        {
            var (px, py) = MinimapGeometry.WorldToPixel(position.X, position.Z, textureSize);

            // Continuous-space flip matching WorldMapRenderer's own
            // discrete row flip (imageRow = size - 1 - y) — the -1 there is
            // purely an integer-index artifact of a 0..size-1 array, not
            // meaningful for a continuous coordinate, so the flip here is
            // the plain linear one over the texture's [0, size] extent.
            var flippedY = textureSize - py;

            var screenX = offsetX + px * scale;
            var screenY = offsetY + flippedY * scale;

            var marker = WorldMapMarkers.Build(kind);
            Canvas.SetLeft(marker, screenX - WorldMapMarkers.Size / 2);
            Canvas.SetTop(marker, screenY - WorldMapMarkers.Size / 2);
            _markers.Children.Add(marker);
        }
    }

    private static IReadOnlyList<(WorldMapMarkerKind Kind, PositionDto Position)> BuildPoints(WorldDto world)
    {
        var points = new List<(WorldMapMarkerKind, PositionDto)>();

        if (world.HaveCustomSpawnPoint)
        {
            points.Add((WorldMapMarkerKind.Spawn, world.SpawnPoint));
        }

        if (world.HaveLogoutPoint)
        {
            points.Add((WorldMapMarkerKind.Logout, world.LogoutPoint));
        }

        if (world.HaveDeathPoint)
        {
            points.Add((WorldMapMarkerKind.Death, world.DeathPoint));
        }

        // No have-flag exists for this one — shown unconditionally,
        // matching WorldsTabModule's own
        // point row, which already treats a zero home point as real rather
        // than "never set."
        points.Add((WorldMapMarkerKind.Home, world.HomePoint));

        return points;
    }

    private static Control BuildLegend()
    {
        var legend = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var kind in LegendOrder)
        {
            legend.Children.Add(BuildLegendItem(kind));
        }

        return legend;
    }

    private static Control BuildLegendItem(WorldMapMarkerKind kind)
    {
        var swatchHost = new Canvas { Width = WorldMapMarkers.Size, Height = WorldMapMarkers.Size };
        var swatch = WorldMapMarkers.Build(kind);
        swatchHost.Children.Add(swatch);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(swatchHost);
        row.Children.Add(new TextBlock { Text = WorldMapMarkers.Label(kind), VerticalAlignment = VerticalAlignment.Center });

        return row;
    }

    /// <summary>Named <c>Open</c>, not <c>Show</c> — a static factory of that
    /// name would hide <see cref="Window"/>'s own instance <c>Show</c>
    /// overloads (CS0108), which the body below still needs to call.
    /// <c>async Task</c>, not <c>void</c>: modal (<see cref="ShowDialog(Window)"/>)
    /// requires awaiting, same shape as <see cref="ConfirmDialog.Ask"/>.</summary>
    internal static async Task Open(Window owner, CharacterEditor editor, WorldDto world, WorldMapDto map, Action onEdited, Action<string> onMessage)
    {
        var window = new WorldMapWindow(editor, world, map, onEdited, onMessage);
        await window.ShowDialog(owner);
    }
}
