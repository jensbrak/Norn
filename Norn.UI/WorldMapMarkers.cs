using Avalonia;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>The four per-world points a character save carries
/// — <see cref="WorldMapWindow"/>'s
/// optional overlay draws one of these per point that's actually set.</summary>
internal enum WorldMapMarkerKind
{
    Spawn,
    Logout,
    Home,
    Death,
}

/// <summary>
/// Shape, color, and label for each <see cref="WorldMapMarkerKind"/> —
/// presentation-only, no wire-format or domain-general
/// stake, so it lives in <c>Norn.UI</c> alongside <see cref="WorldMapRenderer"/>
/// rather than <c>Norn.Adapter</c>.
/// <para>
/// Deliberately four distinct silhouettes (triangle/circle/square/diamond),
/// not one shape recolored four ways — identifiable without relying on
/// color alone, the same reasoning <see cref="WorldMapRenderer"/> already
/// applies by putting "explored by others" in a different hue *family*
/// from "explored," not just a lighter shade of it. Solid fill for
/// <see cref="WorldMapMarkerKind.Spawn"/>/<see cref="WorldMapMarkerKind.Logout"/>,
/// hollow/outline for <see cref="WorldMapMarkerKind.Home"/>/
/// <see cref="WorldMapMarkerKind.Death"/> — an honest secondary signal of
/// which points actually do something in-game (spawn/logout have a real,
/// bounded gameplay effect; home is a
/// waystone-only fallback; death has none at all) versus which are along
/// for the ride.
/// </para>
/// <para>
/// All four are abstract shapes, not literal pictograms — matching
/// <see cref="IconButtons"/>'s own established convention (a symbolic
/// meta-language, not small pictures of the thing). A legend
/// (<see cref="WorldMapWindow"/>) carries the actual spawn/logout/home/death
/// meaning, which is what makes plain geometric shapes sufficient here —
/// they don't need to be individually mnemonic.
/// </para>
/// </summary>
internal static class WorldMapMarkers
{
    /// <summary>Bounding box, in screen pixels — fixed regardless of the
    /// map window's zoom/resize (only a marker's *position* scales with the
    /// image's uniform-stretch transform, never its own size), matching the
    /// small, constant footprint of an <see cref="IconButtons"/> glyph
    /// rather than shrinking to nothing or ballooning with the window.</summary>
    public const double Size = 12;

    private static readonly IBrush SpawnColor = new SolidColorBrush(Color.FromRgb(230, 180, 60));
    private static readonly IBrush LogoutColor = new SolidColorBrush(Color.FromRgb(120, 200, 140));
    private static readonly IBrush HomeColor = new SolidColorBrush(Color.FromRgb(190, 160, 220));
    private static readonly IBrush DeathColor = new SolidColorBrush(Color.FromRgb(210, 90, 90));

    public static string Label(WorldMapMarkerKind kind) => kind switch
    {
        WorldMapMarkerKind.Spawn => "Spawn",
        WorldMapMarkerKind.Logout => "Logout",
        WorldMapMarkerKind.Home => "Home",
        WorldMapMarkerKind.Death => "Death",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static IBrush Brush(WorldMapMarkerKind kind) => kind switch
    {
        WorldMapMarkerKind.Spawn => SpawnColor,
        WorldMapMarkerKind.Logout => LogoutColor,
        WorldMapMarkerKind.Home => HomeColor,
        WorldMapMarkerKind.Death => DeathColor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Solid fill for the two points with a real gameplay effect;
    /// outline-only for the two that don't — see this class's own doc
    /// comment for the reasoning.</summary>
    public static bool IsSolid(WorldMapMarkerKind kind) =>
        kind is WorldMapMarkerKind.Spawn or WorldMapMarkerKind.Logout;

    /// <summary>Builds a <see cref="Path"/> for <paramref name="kind"/>,
    /// centered on its own (0,0) origin — callers translate the whole
    /// element to a screen point (<c>Canvas.Left</c>/<c>Canvas.Top</c>),
    /// never reposition the geometry itself, so centering is exact by
    /// construction rather than measured after layout.</summary>
    public static Avalonia.Controls.Shapes.Path Build(WorldMapMarkerKind kind)
    {
        var solid = IsSolid(kind);
        var brush = Brush(kind);
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = BuildGeometry(kind),
            Fill = solid ? brush : null,
            Stroke = brush,
            StrokeThickness = solid ? 0 : 1.5,
        };
        return path;
    }

    private static Geometry BuildGeometry(WorldMapMarkerKind kind)
    {
        // A small margin inside the Size×Size box on every shape — a solid
        // stroke-only outline drawn flush against its own bounding box reads
        // as clipped rather than deliberately shaped.
        const double r = Size / 2;
        const double margin = 1;
        var inner = r - margin;

        return kind switch
        {
            WorldMapMarkerKind.Spawn => Triangle(r, inner),
            WorldMapMarkerKind.Logout => new EllipseGeometry(new Rect(margin, margin, inner * 2, inner * 2)),
            WorldMapMarkerKind.Home => new RectangleGeometry(new Rect(margin, margin, inner * 2, inner * 2)),
            WorldMapMarkerKind.Death => Diamond(r, inner),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static StreamGeometry Triangle(double center, double radius)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(center, center - radius), isFilled: true);
        ctx.LineTo(new Point(center + radius * 0.87, center + radius * 0.5));
        ctx.LineTo(new Point(center - radius * 0.87, center + radius * 0.5));
        ctx.EndFigure(true);
        return geometry;
    }

    private static StreamGeometry Diamond(double center, double radius)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(center, center - radius), isFilled: true);
        ctx.LineTo(new Point(center + radius, center));
        ctx.LineTo(new Point(center, center + radius));
        ctx.LineTo(new Point(center - radius, center));
        ctx.EndFigure(true);
        return geometry;
    }
}
