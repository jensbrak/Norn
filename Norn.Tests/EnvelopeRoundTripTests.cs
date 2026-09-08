using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// The corpus harness at L0: the whole payload is opaque, only the file frame is
/// interpreted.
/// </summary>
/// <remarks>
/// <para>
/// R1 (byte identity) and R2 (graph stability). At this layer the payload
/// is re-emitted verbatim, so R1 is not weakened by save version and must hold for
/// every corpus file regardless of which version it carries.
/// </para>
/// <para>
/// R1 here is not a tautology: the writer <i>recomputes</i> the SHA-512 rather than
/// carrying the stored hash through, so a green R1 also proves the hash algorithm,
/// its byte range, and both length prefixes. The supplementary tests below exist to
/// tell those causes apart when R1 goes red.
/// </para>
/// </remarks>
public class EnvelopeRoundTripTests
{
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R1_rewriting_a_save_reproduces_it_byte_for_byte(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);
        var original = File.ReadAllBytes(path);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();

        using var rewritten = TempFile.Create();
        new PlayerProfile(rewritten.Path).SavePlayerToDisk(envelope);

        var diff = ByteDiff.Describe(original, File.ReadAllBytes(rewritten.Path));

        Assert.True(diff is null, $"R1 failed for {fileName}.\n{diff}");
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void R2_reading_a_rewritten_save_yields_an_equal_graph(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var first = new PlayerProfile(path).LoadPlayerDataFromDisk();

        using var rewritten = TempFile.Create();
        new PlayerProfile(rewritten.Path).SavePlayerToDisk(first);
        var second = new PlayerProfile(rewritten.Path).LoadPlayerDataFromDisk();

        AssertEqual(first, second, fileName!);
    }

    /// <summary>
    /// Pins the claim that the trailer is SHA-512 over exactly the payload bytes.
    /// R1 depends on it, so when R1 goes red this test says whether the hash was
    /// the reason.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Stored_hash_is_SHA512_over_exactly_the_payload(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();
        var diff = ByteDiff.Describe(envelope.Hash, envelope.ComputeHash());

        Assert.True(
            diff is null,
            $"The stored hash in {fileName} is not SHA-512 over its payload.\n"
            + "The game never verifies this, so a mismatch is a real possibility rather "
            + "than an impossible one — and it would fail R1 for a reason that has nothing "
            + $"to do with the parser.\n{diff}");
    }

    /// <summary>
    /// The game reads the hash length rather than assuming 64, so we do too. This
    /// records that every real file nevertheless carries 64 — if that ever stops
    /// being true, the mirror already handles it and only this expectation moves.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Stored_hash_length_is_64(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();

        Assert.Equal(64, envelope.HashLength);
        Assert.Equal(64, envelope.Hash.Length);
    }

    /// <summary>
    /// The game never reads past the hash and never writes anything there. R3 still
    /// requires the region be captured, but this asserts it is in fact empty — an
    /// unnoticed tail would otherwise show up as a bare length mismatch in R1.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Nothing_follows_the_hash(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();

        Assert.Empty(envelope.TrailingBytes);
    }

    /// <summary>
    /// The frame accounts for the whole file: 4 + payload + 4 + hash == file size.
    /// A short <c>ReadBytes</c> at end of stream returns quietly rather than
    /// throwing, so without this a truncated file would look like a clean parse.
    /// <para>
    /// The declared-vs-delivered hash length is asserted separately, because
    /// the sum alone cannot establish what this comment claims (found in
    /// review): it adds up the lengths the reader actually <i>returned</i>,
    /// so a file declaring a 64-byte hash while carrying only three bytes of
    /// one reconciles perfectly against its own truncated size. Comparing
    /// <see cref="SaveFileEnvelope.HashLength"/> — the length the file
    /// declares — against what came back is what actually catches it. See
    /// <see cref="A_truncated_hash_is_visible_in_the_envelope"/>, which
    /// exercises exactly that file shape.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Frame_accounts_for_every_byte_in_the_file(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);

        var envelope = new PlayerProfile(path).LoadPlayerDataFromDisk();
        var framed = 4 + envelope.PlayerData.Length + 4 + envelope.Hash.Length + envelope.TrailingBytes.Length;

        Assert.Equal(new FileInfo(path).Length, framed);
        Assert.Equal(envelope.HashLength, envelope.Hash.Length);
    }

    /// <summary>
    /// Proves the declared-vs-delivered check above actually bites, on a
    /// deliberately truncated copy of a tracked fixture — the file shape the
    /// frame-size sum by itself cannot distinguish from a clean parse.
    /// Fixture-based, so it runs on a fresh clone with no corpus present.
    /// </summary>
    [Fact]
    public void A_truncated_hash_is_visible_in_the_envelope()
    {
        var source = TestPaths.FixtureFiles().FirstOrDefault();
        Assert.SkipWhen(source is null, "No .fch fixture present.");

        using var scratch = TempFile.Create();
        var bytes = File.ReadAllBytes(source!);
        File.WriteAllBytes(scratch.Path, bytes[..^3]);

        var envelope = new PlayerProfile(scratch.Path).LoadPlayerDataFromDisk();

        Assert.Equal(3, envelope.HashLength - envelope.Hash.Length);
    }

    private static void AssertEqual(SaveFileEnvelope expected, SaveFileEnvelope actual, string fileName)
    {
        var payloadDiff = ByteDiff.Describe(expected.PlayerData, actual.PlayerData);
        Assert.True(payloadDiff is null, $"R2 failed for {fileName}: payload differs.\n{payloadDiff}");

        var hashDiff = ByteDiff.Describe(expected.Hash, actual.Hash);
        Assert.True(hashDiff is null, $"R2 failed for {fileName}: hash differs.\n{hashDiff}");

        Assert.Equal(expected.HashLength, actual.HashLength);
        Assert.Equal(expected.TrailingBytes, actual.TrailingBytes);
    }
}
