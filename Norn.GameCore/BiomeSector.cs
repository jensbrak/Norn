namespace Norn.GameCore;

// mirrors: BiomeSector
// source:  Valheim 1.0.7
// note:    STUB, NEW AT 1.0.7. BiomeSector is a gameplay type (biome sectors,
//          alt-biome overrides, world-level naming) and none of it is on a
//          load/save path. It exists here for exactly one static method, which
//          1.0.7 made reachable from Player.Load as the known-biome read
//          migration. Nothing else from the type is carried.
// note:    Not mirrored, and not reachable from any read path: GetName(bool),
//          the AltBiomes list and its m_nameOverride handling, and every
//          instance member. GetName calls GetBiomeName below and then layers
//          alt-biome overrides on top — that layering is live-world state with
//          no counterpart in a save file, so a mirror of it would have nothing
//          to read from.
public static class BiomeSector
{
    // mirrors: BiomeSector.GetBiomeName(Heightmap.Biome)
    // source:  Valheim 1.0.7
    // note:    THIS IS A READ MIGRATION, NOT A DISPLAY HELPER, and that
    //          distinction is why it lives in GameCore rather than under
    //          a presentation-only constant. 1.0.7 changed Player.m_knownBiome's wire type
    //          from int to string; a save written below that boundary stores
    //          ints, and the only way to re-emit it through the new writer is
    //          to convert. Skipping the conversion — the way the mirror
    //          deliberately skips the v < 27 flametal rename — is not available
    //          here, because there is no int-writing path left to fall back to.
    // note:    ToString() on a [Flags] enum returns a comma-and-space-joined
    //          member list for any value that is not a single declared member,
    //          and this method lowercases and prefixes that wholesale. Source
    //          does not validate the input, so neither does this. Do not
    //          "fix" it — the output is the migrated value and has to match
    //          what the game itself would have produced.
    public static string GetBiomeName(Biome biome)
    {
        return "$biome_" + biome.ToString().ToLower();
    }
}
