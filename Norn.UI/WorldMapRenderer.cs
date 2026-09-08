using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Turns a decoded <see cref="WorldMapDto"/> into a disc-shaped bitmap — a
/// POC fog-of-war render, not an attempt at the game's own look (no
/// terrain/biome data exists in a character save to draw from — only the
/// explored/unexplored bit per tile). Three tones: unexplored, explored by
/// this character, and — when <paramref name="includeOthers"/> is true —
/// explored only via a shared source (a cartography table), matching the
/// game's own toggle between "my exploration" and "my exploration + what's
/// been shared with me." A cell this character explored directly always
/// renders as such regardless of the toggle; the toggle only ever adds
/// cells, never changes how an already-own-explored cell looks.
/// <para>
/// The disc shape isn't stylistic cropping — <see cref="MinimapGeometry"/>'s
/// world-edge radius is real (a decompiled source constant, corroborated
/// independently), so the boundary drawn here is where the playable world
/// genuinely ends. Everything in <see cref="WorldMapDto.Explored"/>/
/// <see cref="WorldMapDto.ExploredOthers"/> outside that radius is texture
/// margin the game's own minimap always carries (the texture's half-extent,
/// 12288 world units at the confirmed <c>m_pixelSize</c>, is comfortably
/// past the world edge) — masking it out shows the map "in relation to
/// center and edge" rather than a square that includes dead space no
/// in-game view ever shows.
/// </para>
/// </summary>
/// <remarks>
/// <c>Minimap.Decode</c>'s array stays exactly as the wire format stores it
/// (row-major, <c>y * TextureSize + x</c>, y = 0 at the world's most-negative-Z
/// edge — confirmed against
/// <c>Minimap.WorldToPixel</c>/<c>Explore</c>/<c>ResetAndExplore</c>, 2026-08-24).
/// Unity's own texture convention displays row 0 at the BOTTOM (confirmed the
/// same pass: <c>Texture2D.SetPixel</c>/<c>GetPixels</c>/<c>SetPixels</c> are
/// bottom-row-first, and pin/player-marker screen placement is driven by the
/// same unflipped y value, so nothing downstream in the game re-flips it
/// either). This renderer reproduces that here at rasterization time only —
/// array row <c>y</c> is written to image row <c>size - 1 - y</c> — rather
/// than flipping the decoded array itself, which would silently break
/// anything else that ever indexes <see cref="WorldMapDto.Explored"/> by its
/// documented row-major contract. The disc mask is unaffected by this flip
/// either way — a circle centered on the texture's own center is symmetric
/// under a vertical mirror, so it needs no corresponding adjustment.
/// </remarks>
internal static class WorldMapRenderer
{
    // note: NOT IN SOURCE — presentation-only constants, not
    // wire-format facts. Approximate, not sampled from game assets: a dark
    // fog tone, a parchment-tan "own exploration" tone (picked to read as
    // "map-like" without claiming to reproduce the game's actual palette),
    // and a cooler blue-grey "explored by others" tone, deliberately in a
    // different hue family from the tan (not just a lighter/darker shade of
    // it) so the two read as distinct kinds of information at a glance.
    private static readonly byte[] UnexploredBgra = [20, 20, 26, 255];
    private static readonly byte[] ExploredBgra = [108, 150, 180, 255];
    private static readonly byte[] ExploredByOthersBgra = [190, 150, 140, 255];

    /// <summary>Fully transparent — requires <see cref="AlphaFormat.Premul"/>
    /// below (BGR must already be zeroed at alpha 0, which a plain [0,0,0,0]
    /// already satisfies without further premultiplication math).</summary>
    private static readonly byte[] OutsideWorldBgra = [0, 0, 0, 0];

    public static WriteableBitmap Render(WorldMapDto map, bool includeOthers)
    {
        var size = map.TextureSize;
        var center = MinimapGeometry.CenterPixel(size);
        var pixelRadiusSquared = MinimapGeometry.PixelRadius * MinimapGeometry.PixelRadius;

        var bitmap = new WriteableBitmap(
            new PixelSize(size, size),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var buffer = bitmap.Lock();
        var row = new byte[buffer.RowBytes];

        for (var y = 0; y < size; y++)
        {
            var dy = y - center;
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                byte[] color;
                if (dx * dx + dy * dy > pixelRadiusSquared)
                {
                    color = OutsideWorldBgra;
                }
                else
                {
                    var i = y * size + x;
                    if (map.Explored[i] != 0)
                    {
                        color = ExploredBgra;
                    }
                    else if (includeOthers && map.ExploredOthers[i] != 0)
                    {
                        color = ExploredByOthersBgra;
                    }
                    else
                    {
                        color = UnexploredBgra;
                    }
                }

                Array.Copy(color, 0, row, x * 4, 4);
            }

            var imageRow = size - 1 - y;
            Marshal.Copy(row, 0, buffer.Address + imageRow * buffer.RowBytes, buffer.RowBytes);
        }

        return bitmap;
    }
}
