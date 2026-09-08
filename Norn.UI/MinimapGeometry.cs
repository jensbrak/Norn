namespace Norn.UI;

// note: NOT IN SOURCE — presentation-only geometry, not a
// wire-format fact: GameCore's decoded arrays round-trip correctly with no
// knowledge of where the playable world actually ends. Serves exactly one
// feature (the map disc render), same reasoning as AppearanceColors.cs —
// belongs in Norn.UI, not GameCore/Adapter, and carries no mirror-discipline
// obligation.
//
// game-derived: Minimap.m_pixelSize (Unity-serialized prefab field, not in
// decompiled source — same situation m_textureSize was in) = 12, confirmed
// via AssetRipper export
// d:/Valheim/rip/0.221.10/ExportedProject/Assets/PrefabInstance/IngameGui_HUD_Minimap.prefab:1690,
// the same file/line-adjacent field already used for m_textureSize.
//
// game-derived: world edge radius = WorldGenerator.waterEdge (Valheim
// 0.221.10 AND 0.221.4, d:/Valheim/src/<version>/assembly_valheim/WorldGenerator.cs)
// = 10500f, the generator's own hard terrain-generation cutoff — a real
// decompiled source constant, not an inference. Independently corroborated,
// not derived from: a sibling project's (`Peeak`, `c:\zon3\Apps\Peeak`)
// live-tested BepInEx plugin constant `WorldRadius = 10480f`
// ("nothing useful lives past this radius" per its own comment, checked
// against a running WorldGenerator, not just read from source).
internal static class MinimapGeometry
{
    public const float PixelSize = 12f;

    public const float WorldEdgeRadius = 10500f;

    /// <summary>The world edge's radius, in texture pixels — independent of
    /// texture size, since it's purely a world-units-to-pixels conversion.</summary>
    public static float PixelRadius => WorldEdgeRadius / PixelSize;

    /// <summary>Where world (0,0) falls in a <paramref name="textureSize"/>
    /// square texture. Always the exact center: <c>Minimap.WorldToPixel</c>
    /// (confirmed in the decompiled source) offsets by exactly half the
    /// texture size on both axes, with no off-center bias.</summary>
    public static float CenterPixel(int textureSize) => textureSize / 2f;

    /// <summary>World (x, z) to texture-pixel coordinates, in the same
    /// unflipped space <see cref="WorldMapRenderer"/> decodes into —
    /// <c>Minimap.WorldToPixel</c>'s own formula (confirmed in the
    /// decompiled source): <c>x / PixelSize
    /// + center</c>, same for z→y. Row 0 in this space is the world's
    /// most-negative-Z edge; a caller placing this on the rendered bitmap
    /// must apply the same row-flip <see cref="WorldMapRenderer"/> already
    /// does at rasterization time (<c>size - 1 - y</c>) — this method
    /// doesn't do that itself, since it has no opinion on display, only on
    /// the world-to-texture conversion.</summary>
    public static (float X, float Y) WorldToPixel(float worldX, float worldZ, int textureSize)
    {
        var center = CenterPixel(textureSize);
        return (worldX / PixelSize + center, worldZ / PixelSize + center);
    }
}
