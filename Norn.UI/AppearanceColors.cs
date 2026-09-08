using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Valheim's own character-appearance color mechanism, reimplemented here as
/// plain feature code — not <c>GameCore</c>, not subject to mirror-discipline.
/// These six constants aren't wire-format facts: <c>Player.m_skinColor</c>/
/// <c>m_hairColor</c> already round-trip correctly with zero knowledge of
/// them. They only serve this one editing feature, which is why they live in
/// <c>Norn.UI</c> rather than <c>GameCore</c> or <c>Adapter</c>: which
/// colors a slider can reach is presentation-specific, serving exactly one
/// control, not a fact either the read or write path needs.
/// </summary>
/// <remarks>
/// Sourced from Unity-serialized fields on <c>PlayerCustomizaton</c>
/// (script GUID <c>c1b78fa918b030faf1c1f6f6164daeb2</c>) — not decompiled
/// C#, which only shows placeholder white initializers for these. Extracted
/// from a re-imported AssetRipper export, identical across all three
/// occurrences checked (character creation, the barber prefab, the in-scene
/// barber copy). Valheim 0.221.10. Each constant below carries a
/// <c>game-derived:</c> tag naming what to re-check on a future patch.
/// </remarks>
internal static class AppearanceColors
{
    // game-derived: all six constants below are one unit, always
    // re-verified together (same script GUID, same three occurrences).
    public static readonly ColorDto SkinColor0 = new(1f, 1f, 1f);
    public static readonly ColorDto SkinColor1 = new(0.3f, 0.3f, 0.3f);
    public static readonly ColorDto HairColor0 = new(1f, 0.9310345f, 0.7058823f);
    public static readonly ColorDto HairColor1 = new(1f, 0.48813385f, 0.2794118f);
    public const float HairMinLevel = 0.1f;
    public const float HairMaxLevel = 1f;

    /// <summary>
    /// Mirrors <c>PlayerCustomizaton.Update()</c>'s skin-color computation:
    /// <c>Color.Lerp(m_skinColor0, m_skinColor1, hueSlider.value)</c>.
    /// <paramref name="hue"/> is expected in [0, 1]; clamped defensively
    /// since nothing upstream in Norn's own slider guarantees that range by
    /// construction the way Avalonia's <c>Slider.Minimum</c>/<c>Maximum</c>
    /// do at the control level.
    /// </summary>
    public static ColorDto SkinColorAt(float hue) => Lerp(SkinColor0, SkinColor1, Clamp01(hue));

    /// <summary>
    /// Mirrors <c>PlayerCustomizaton.Update()</c>'s hair-color computation:
    /// a tone lerp between the two endpoint colors, scaled by a level lerp
    /// between <see cref="HairMinLevel"/> and <see cref="HairMaxLevel"/>.
    /// Both parameters expected in [0, 1], clamped defensively.
    /// </summary>
    public static ColorDto HairColorAt(float tone, float level)
    {
        var baseColor = Lerp(HairColor0, HairColor1, Clamp01(tone));
        var scalar = Lerp(HairMinLevel, HairMaxLevel, Clamp01(level));
        return new ColorDto(baseColor.R * scalar, baseColor.G * scalar, baseColor.B * scalar);
    }

    /// <summary>
    /// Reverse-solves the skin slider position that best approximates
    /// <paramref name="target"/> — so a freshly opened tab's slider starts
    /// where the character's actual color already is, not at a fixed
    /// anchor unrelated to it (found unusable in practice). Brute-force nearest
    /// match by L1 distance over a fine grid, the same *kind* of search the
    /// game's own barber does when sitting down — reused deliberately as
    /// "anchored in game behavior we already
    /// documented," not invented fresh. Does not reproduce the barber's own
    /// bug (conflating a normalized interpolant with an absolute level);
    /// there is no absolute-value output here to conflate, only a single
    /// normalized position. 201 steps is far finer than the barber's 0.02
    /// (50 steps) and costs microseconds — precision was cheap to add once
    /// doing the search at all, so there was no reason to match the game's
    /// own coarser grid.
    /// </summary>
    public static float SolveSkinHue(ColorDto target)
    {
        var bestHue = 0f;
        var bestDistance = float.MaxValue;
        for (var i = 0; i <= 200; i++)
        {
            var hue = i / 200f;
            var distance = L1Distance(SkinColorAt(hue), target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHue = hue;
            }
        }

        return bestHue;
    }

    /// <summary>Same reasoning as <see cref="SolveSkinHue"/>, for hair's two parameters — a 201×201 grid, still microseconds.</summary>
    public static (float Tone, float Level) SolveHairToneLevel(ColorDto target)
    {
        var bestTone = 0f;
        var bestLevel = 1f;
        var bestDistance = float.MaxValue;
        for (var ti = 0; ti <= 200; ti++)
        {
            var tone = ti / 200f;
            for (var li = 0; li <= 200; li++)
            {
                var level = li / 200f;
                var distance = L1Distance(HairColorAt(tone, level), target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTone = tone;
                    bestLevel = level;
                }
            }
        }

        return (bestTone, bestLevel);
    }

    /// <summary>
    /// Norn's own readout convention, deliberately not Loki-templated —
    /// web-style hex first, then the
    /// stored float triple in parentheses, Vector3-display-style:
    /// <c>#8C6345 (0.550, 0.390, 0.271)</c>. Hex is byte-quantized; the
    /// parenthesized triple carries the exact stored precision.
    /// </summary>
    public static string FormatColor(ColorDto color) =>
        $"#{ToByte(color.R):X2}{ToByte(color.G):X2}{ToByte(color.B):X2} ({color.R:0.000}, {color.G:0.000}, {color.B:0.000})";

    // Rounds, not truncates (found in review): a lerp/solve result like
    // 0.99999f — common floating-point residue from SolveSkinHue/
    // SolveHairToneLevel — clamps to 254.99..., which a bare (byte) cast
    // truncates to 254 instead of the 255 it should round to, showing one
    // shade darker than the actual stored color.
    private static byte ToByte(float channel) => (byte)MathF.Round(Math.Clamp(channel * 255f, 0f, 255f));

    private static float L1Distance(ColorDto a, ColorDto b) =>
        Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    private static ColorDto Lerp(ColorDto a, ColorDto b, float t) =>
        new(Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t));

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
