using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// R1/R2 against the tracked fixture set, independent of the gitignored real
/// corpus entirely — the actual thing this fixture set exists to prove: a
/// public clone with no <c>reference/</c> directory still gets to see Goal A's
/// mechanical proof run, not skip.
/// </summary>
/// <remarks>
/// Deliberately not sharing <see cref="EnvelopeRoundTripTests"/>' theory
/// source: unlike the real corpus, an empty fixture set is not a normal,
/// expected state to skip past — if <see cref="TestPaths.FixtureFiles"/>
/// comes back empty, that is itself a defect worth a loud failure, not a
/// silent skip. The R1/R2 bodies below mirror
/// <see cref="EnvelopeRoundTripTests"/>' own exactly, at the same envelope
/// layer, since this is the same property against a different file set.
/// </remarks>
public class FixtureRoundTripTests
{
    public static IEnumerable<object[]> Fixtures() =>
        TestPaths.FixtureFiles().Select(path => new object[] { Path.GetFileName(path) });

    [Fact]
    public void The_fixture_set_is_actually_present()
    {
        Assert.NotEmpty(TestPaths.FixtureFiles());
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void R1_rewriting_a_fixture_reproduces_it_byte_for_byte(string fileName)
    {
        var path = Path.Combine(TestPaths.FixtureDirectory!, fileName);
        var original = File.ReadAllBytes(path);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();

        using var rewritten = TempFile.Create();
        new PlayerProfile(rewritten.Path).SavePlayerToDisk(envelope);

        var diff = ByteDiff.Describe(original, File.ReadAllBytes(rewritten.Path));

        Assert.True(diff is null, $"R1 failed for {fileName}.\n{diff}");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void R2_reading_a_rewritten_fixture_yields_an_equal_graph(string fileName)
    {
        var path = Path.Combine(TestPaths.FixtureDirectory!, fileName);
        var first = new PlayerProfile(path).LoadPlayerDataFromDisk();

        using var rewritten = TempFile.Create();
        new PlayerProfile(rewritten.Path).SavePlayerToDisk(first);
        var second = new PlayerProfile(rewritten.Path).LoadPlayerDataFromDisk();

        var payloadDiff = ByteDiff.Describe(first.PlayerData, second.PlayerData);
        Assert.True(payloadDiff is null, $"R2 failed for {fileName}: payload differs.\n{payloadDiff}");

        var hashDiff = ByteDiff.Describe(first.Hash, second.Hash);
        Assert.True(hashDiff is null, $"R2 failed for {fileName}: hash differs.\n{hashDiff}");

        Assert.Equal(first.HashLength, second.HashLength);
        Assert.Equal(first.TrailingBytes, second.TrailingBytes);
    }
}
