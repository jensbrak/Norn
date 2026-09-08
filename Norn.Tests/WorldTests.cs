using Norn.GameCore;
using GameVersion = Norn.GameCore.Version;
using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="World.Load"/> against synthetic payloads covering
/// every version gate on its read path — no real <c>.fwl</c> corpus needed
/// for this, since the fields involved are small enough to construct
/// directly (contrast <see cref="WorldCorpusTests"/>, which needs real
/// files). Follows the mirror discipline's gate-boundary emphasis: each test
/// targets one specific gate crossing.
/// </summary>
public class WorldTests
{
    private const string SampleName = "Hearthhold";
    private const string SampleSeedName = "8f3ha2";
    private const int SampleSeed = 123456;
    private const long SampleUid = 3725283970L;

    // Both bounds are derived from the mirror's own compatibility range rather
    // than written as literals. The upper case was a hardcoded 38 until 1.0.7 —
    // one past the then-ceiling of 37 — and 1.0.7 moved the ceiling to 41,
    // which made 38 a *valid* version. The test then fed a version-38 payload
    // with no body and got an EndOfStreamException instead of a clean refusal:
    // a stale test expectation masquerading as a parser crash.
    [Theory]
    [InlineData((int)GameVersion.World.OldestForwardCompatible - 1)]
    [InlineData((int)GameVersion.c_WorldVersion + 1)]
    public void Load_rejects_an_incompatible_version(int version)
    {
        var payload = new ZPackage();
        payload.Write(version);
        // Mirrors World.LoadWorld's own "reads nothing further" early
        // return — no further bytes are written on purpose.

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.False(loaded);
    }

    [Fact]
    public void Load_reads_the_ungated_fields_at_the_oldest_compatible_version()
    {
        var payload = new ZPackage();
        payload.Write(9);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Equal(SampleName, world.m_name);
        Assert.Equal(SampleSeedName, world.m_seedName);
        Assert.Equal(SampleSeed, world.m_seed);
        Assert.Equal(SampleUid, world.m_uid);
        Assert.Equal(0, world.m_worldGenVersion);
        Assert.False(world.m_needsDB);
        Assert.Empty(world.m_startingGlobalKeys);
    }

    [Fact]
    public void Load_reads_worldGenVersion_once_its_gate_is_reached()
    {
        var payload = new ZPackage();
        payload.Write(26);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Equal(2, world.m_worldGenVersion);
        Assert.False(world.m_needsDB);
        Assert.Empty(world.m_startingGlobalKeys);
    }

    [Fact]
    public void Load_reads_needsDB_once_its_gate_is_reached()
    {
        var payload = new ZPackage();
        payload.Write(30);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);
        payload.Write(true);

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.True(world.m_needsDB);
        Assert.Empty(world.m_startingGlobalKeys);
    }

    [Fact]
    public void Load_reads_startingGlobalKeys_once_its_gate_is_reached()
    {
        var payload = new ZPackage();
        payload.Write(32);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);
        payload.Write(true);
        payload.Write(2);
        payload.Write("NoMap");
        payload.Write("Hildir");

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Equal(["NoMap", "Hildir"], world.m_startingGlobalKeys);
    }

    // Was pinned at 37 until 1.0.7 and kept passing after the ceiling moved to
    // 41 — it just stopped meaning what its name says, and left the block that
    // 41 added with no coverage at all. Derived from the constant now.
    [Fact]
    public void Load_reads_the_newest_known_version_with_every_field_present()
    {
        var payload = new ZPackage();
        payload.Write((int)GameVersion.c_WorldVersion);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);
        payload.Write(false);
        payload.Write(0);
        payload.Write(0);

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Equal((int)GameVersion.c_WorldVersion, world.m_worldVersion);
        Assert.Empty(world.m_playerHistory);
    }

    // The player-history block is the only thing world version 41 added, and
    // the .fwl2 in the corpus has an empty one — so without this, the four
    // strings per record would be entirely untested.
    [Fact]
    public void Load_reads_the_player_history_block_once_its_gate_is_reached()
    {
        var payload = new ZPackage();
        payload.Write((int)GameVersion.World.DeepNorth);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);
        payload.Write(false);
        payload.Write(0);
        payload.Write(2);
        payload.Write("Steam_76561198000000000");
        payload.Write("Hafgrim");
        payload.Write("Hafgrim (server)");
        payload.Write("playfab-aaa");
        payload.Write("Steam_76561198000000001");
        payload.Write("Sigrun");
        payload.Write("");
        payload.Write("playfab-bbb");

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Equal(2, world.m_playerHistory.Count);

        Assert.Equal("Steam_76561198000000000", world.m_playerHistory[0].m_id);
        Assert.Equal("Hafgrim", world.m_playerHistory[0].m_displayName);
        Assert.Equal("Hafgrim (server)", world.m_playerHistory[0].m_serverAssignedDisplayName);
        Assert.Equal("playfab-aaa", world.m_playerHistory[0].m_playfabId);

        Assert.Equal("Steam_76561198000000001", world.m_playerHistory[1].m_id);
        Assert.Equal("Sigrun", world.m_playerHistory[1].m_displayName);
        Assert.Equal("", world.m_playerHistory[1].m_serverAssignedDisplayName);
        Assert.Equal("playfab-bbb", world.m_playerHistory[1].m_playfabId);
    }

    // One below the gate: the same trailing bytes must not be consumed.
    [Fact]
    public void Load_ignores_the_player_history_block_below_its_gate()
    {
        var payload = new ZPackage();
        payload.Write((int)GameVersion.World.ChunkedSave);
        payload.Write(SampleName);
        payload.Write(SampleSeedName);
        payload.Write(SampleSeed);
        payload.Write(SampleUid);
        payload.Write(2);
        payload.Write(false);
        payload.Write(0);

        var world = new World();
        var loaded = world.Load(payload.GetArray());

        Assert.True(loaded);
        Assert.Empty(world.m_playerHistory);
    }
}
