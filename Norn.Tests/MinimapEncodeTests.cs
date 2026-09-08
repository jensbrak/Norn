using Norn.GameCore;
using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// A synthetic, corpus-independent counterpart to <see cref="MinimapDecodeTests"/>:
/// exercises <see cref="Minimap.Encode"/> against a small hand-built
/// <see cref="Minimap.MapData"/> rather than a real save file, so this runs
/// even when <c>reference/save-files</c> is absent. Only the R2 property
/// <see cref="Minimap.Encode"/> can actually claim is asserted here
/// — Decode(Encode(x)) reproduces x,
/// never byte-identity against a game-written blob.
/// </summary>
public class MinimapEncodeTests
{
    [Fact]
    public void Decode_of_Encode_reproduces_texture_size_masks_pins_and_public_position()
    {
        const int textureSize = 4;
        var cellCount = textureSize * textureSize;

        var explored = new byte[cellCount];
        var exploredOthers = new byte[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            explored[i] = (byte)(i % 2);
            exploredOthers[i] = (byte)(i % 3 == 0 ? 1 : 0);
        }

        var original = new Minimap.MapData
        {
            Version = 6, // deliberately not MAPVERSION — Encode always emits the current constant regardless.
            TextureSize = textureSize,
            Explored = explored,
            ExploredOthers = exploredOthers,
            Pins =
            [
                new Minimap.PinData
                {
                    m_name = "Home",
                    m_type = Minimap.PinType.Bed,
                    m_pos = new Vector3(1.5f, 2.5f, -3.5f),
                    m_ownerID = 0L,
                    m_author = "",
                    m_checked = false,
                },
                new Minimap.PinData
                {
                    m_name = "Shared pin",
                    m_type = Minimap.PinType.Boss,
                    m_pos = new Vector3(-100f, 0f, 100f),
                    m_ownerID = 76561197960287930L,
                    m_author = "someone",
                    m_checked = true,
                },
            ],
            PublicReferencePosition = true,
        };

        var encoded = Minimap.Encode(original);
        var decoded = Minimap.Decode(encoded);

        Assert.Equal(Minimap.MAPVERSION, decoded.Version);
        Assert.Equal(original.TextureSize, decoded.TextureSize);
        Assert.Equal(original.Explored, decoded.Explored);
        Assert.Equal(original.ExploredOthers, decoded.ExploredOthers);
        Assert.Equal(original.PublicReferencePosition, decoded.PublicReferencePosition);

        Assert.Equal(original.Pins.Count, decoded.Pins.Count);
        for (int i = 0; i < original.Pins.Count; i++)
        {
            Assert.Equal(original.Pins[i].m_name, decoded.Pins[i].m_name);
            Assert.Equal(original.Pins[i].m_type, decoded.Pins[i].m_type);
            Assert.Equal(original.Pins[i].m_pos, decoded.Pins[i].m_pos);
            Assert.Equal(original.Pins[i].m_ownerID, decoded.Pins[i].m_ownerID);
            Assert.Equal(original.Pins[i].m_author, decoded.Pins[i].m_author);
            Assert.Equal(original.Pins[i].m_checked, decoded.Pins[i].m_checked);
        }
    }

    [Fact]
    public void Encode_second_mask_loop_follows_Explored_length_matching_sources_own_quirk()
    {
        // Explored/ExploredOthers are always allocated at the same length by
        // Decode, but Encode's own provenance header documents mirroring the
        // source's literal quirk of bounding both write loops on
        // m_explored.Length. A mismatched-length ExploredOthers (unreachable
        // via Decode, reachable only by a hand-built MapData like this one)
        // proves the loop bound really is Explored.Length, not
        // ExploredOthers.Length.
        var data = new Minimap.MapData
        {
            TextureSize = 2,
            Explored = [1, 1, 1, 1],
            ExploredOthers = [1, 1, 1, 1, 1, 1], // longer than Explored — extra entries must be ignored on write.
            Pins = [],
            PublicReferencePosition = false,
        };

        var decoded = Minimap.Decode(Minimap.Encode(data));

        Assert.Equal(4, decoded.ExploredOthers.Length);
        Assert.All(decoded.ExploredOthers, b => Assert.Equal(1, b));
    }
}
