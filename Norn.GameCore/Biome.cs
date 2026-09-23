namespace Norn.GameCore;

// mirrors: Heightmap.Biome (nested enum only — Heightmap itself, the Unity
// terrain component, is not mirrored)
// source:  Valheim 1.0.15
// note:    [Flags], explicit values. Gap at 128 is in source too — All (895)
//          is the sum of the nine single-bit members and does not include it.
//          Stored values are single-bit identities, not masks; nothing in the
//          load code validates this.
// note:    Land = 639 is NEW at 1.0.7, and it changes observable behaviour in
//          exactly one place: BiomeSector.GetBiomeName runs ToString() over
//          this enum, so a legacy save storing the value 639 now migrates to
//          "$biome_land" instead of the comma-joined member list it would have
//          produced before. Vanishingly unlikely in a real save — 639 is a
//          mask, and stored values are single-bit — but it is a real change,
//          not a cosmetic one. All = 895 is unchanged.
// note:    NO LONGER THE STORED TYPE. Through 0.221.10 Player.m_knownBiome was
//          a set of these values and they went to disk as ints. 1.0.7 changed
//          the field to strings; this enum now survives only as the input to
//          the legacy read migration (player-data version 18..30/32), never as
//          something written. See Player.Load.

/// <summary>Biome flags, as stored in player data below the 1.0.7 string migration.</summary>
[Flags]
public enum Biome
{
    None = 0,
    Meadows = 1,
    Swamp = 2,
    Mountain = 4,
    BlackForest = 8,
    Plains = 16,
    AshLands = 32,
    DeepNorth = 64,
    Ocean = 256,
    Mistlands = 512,
    All = 895,
    Land = 639
}
