using Norn.GameCore;
using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// A dedicated fixture (not a theory over <see cref="Corpus.Files"/> or
/// <see cref="FixtureRoundTripTests"/>'s own set), because only this one file
/// carries what this test needs: a world whose decompressed map size was
/// independently captured from the game's own console log
/// (<c>"Minimap: unpacking compressed mapData 25416 =&gt; 8395602 bytes"</c>).
/// This cross-checks <see cref="Minimap.Decode"/> against that real, external
/// measurement rather than only against itself.
/// </summary>
/// <remarks>
/// A tracked public fixture (<c>fixture-v43-known-map-size.fch</c>), not the
/// gitignored corpus — the specific byte-size claim below only means anything
/// against one exact, unchanging file, so this needs to ship rather than be
/// re-derived per contributor. Copied byte-for-byte from the original corpus
/// file with no genericization pass: unlike the player-name fixtures, nothing
/// here reads or displays the player's identity, and re-serializing it
/// through the app's write path — the usual genericization route — was
/// avoided deliberately, since the source file predates the current profile
/// writer version and a version-upgrading rewrite would risk silently
/// changing the very map bytes this test verifies.
/// </remarks>
public class MinimapDecodeTests
{
    private const long FixtureWorldId = 3725283970;
    private const int ExpectedDecompressedInnerSize = 8395602;

    [Fact]
    public void Decodes_a_known_worlds_map_matching_the_games_own_console_log()
    {
        var path = Path.Combine(TestPaths.FixtureDirectory ?? "", "fixture-v43-known-map-size.fch");
        Assert.True(File.Exists(path), $"{path} not found — this is a tracked fixture, expected in every checkout.");

        var profile = new PlayerProfile(path);
        var loaded = profile.Load();
        Assert.True(loaded, $"fixture failed to load (profile version {profile.ProfileVersion}).");

        var worldData = profile.m_worldData
            .Where(pair => pair.Key == FixtureWorldId)
            .Select(pair => pair.Value)
            .FirstOrDefault();
        Assert.True(worldData is not null,
            $"World {FixtureWorldId} not present in this fixture — it may have drifted from the original.");
        Assert.NotNull(worldData!.m_mapData);

        // Independent cross-check: decompress the outer blob's inner package
        // ourselves (bypassing Minimap.Decode) and compare its raw size against
        // the console log's own reported figure, before trusting Decode's own
        // interpretation of the same bytes.
        var outer = new ZPackage(worldData.m_mapData);
        var version = outer.ReadInt();
        Assert.True(version >= 7, $"Expected a gzip-compressed blob (version >= 7), got version {version}.");
        var inner = outer.ReadCompressedPackage();
        Assert.Equal(ExpectedDecompressedInnerSize, inner.Size());

        var mapData = Minimap.Decode(worldData.m_mapData);
        Assert.Equal(2048, mapData.TextureSize);
        Assert.Equal(2048 * 2048, mapData.Explored.Length);
        Assert.Equal(2048 * 2048, mapData.ExploredOthers.Length);
        Assert.Contains(mapData.Explored, b => b != 0);
    }
}
