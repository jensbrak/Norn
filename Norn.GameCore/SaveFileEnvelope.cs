using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: nothing — this type has no counterpart in the game.
// source:  Valheim 1.0.15
// note:    The game is not a round-tripper. PlayerProfile.LoadPlayerDataFromDisk
//          reads the hash length and the hash purely to consume them and throws
//          both away, and it never looks past the hash at all. A byte-identity
//          oracle cannot throw anything away (rule R3), so the discarded parts
//          are captured here. The read ORDER and the reads themselves are
//          unchanged — this type is where the bytes land, not a different way of
//          reading them.

/// <summary>
/// The <c>.fch</c> file frame, in full. No magic number, no signature, no
/// compression, no padding — a bare frame written by a <see cref="BinaryWriter"/>:
/// </summary>
/// <remarks>
/// <code>
/// offset 0                  Int32                 payload length N
/// offset 4                  byte[N]               payload (the profile ZPackage)
/// offset 4 + N              Int32                 hash length H
/// offset 8 + N              byte[H]               SHA-512 over the payload
/// offset 8 + N + H          EOF
/// </code>
/// <para>
/// The envelope carries no version of its own. The profile version is the first
/// <c>Int32</c> <i>inside</i> the payload, so nothing here is version-gated.
/// </para>
/// <para>
/// At this layer the payload is opaque: <see cref="PlayerData"/> is held and re-emitted
/// verbatim, which is what makes R1 hold from the first commit.
/// </para>
/// </remarks>
public sealed class SaveFileEnvelope
{
    public SaveFileEnvelope(byte[] playerData, int hashLength, byte[] hash, byte[] trailingBytes)
    {
        PlayerData = playerData;
        HashLength = hashLength;
        Hash = hash;
        TrailingBytes = trailingBytes;
    }

    /// <summary>
    /// The payload: the profile <see cref="ZPackage"/>'s bytes, opaque at this layer.
    /// Its own first <c>Int32</c> is the profile version.
    /// </summary>
    public byte[] PlayerData { get; }

    /// <summary>
    /// The hash length as stored on disk. The game always writes 64 and never
    /// reads this back for anything, but it reads it rather than assuming it, so
    /// a hand-edited file with a different value is still consumed correctly.
    /// Kept for the same reason.
    /// </summary>
    public int HashLength { get; }

    /// <summary>
    /// The hash as stored on disk: SHA-512 over exactly the payload bytes —
    /// covering the payload's own leading version <c>Int32</c>, excluding both
    /// length prefixes and the hash field itself.
    /// </summary>
    /// <remarks>
    /// <b>Never verified on load.</b> The game reads these bytes and discards the
    /// result: a <c>.fch</c> with a corrupt hash still loads, with no signal
    /// either way. Verification must not be added to the load path — it would
    /// reject files the game accepts. <see cref="ComputeHash"/> exists so a test
    /// can compare the two deliberately.
    /// </remarks>
    public byte[] Hash { get; }

    /// <summary>
    /// Anything after the hash. The game writes none and never reads past the
    /// hash, so this is expected to be empty — but R3 requires every byte on disk
    /// to be accounted for, and an unread tail would silently break R1.
    /// </summary>
    public byte[] TrailingBytes { get; }

    /// <summary>
    /// SHA-512 over <see cref="PlayerData"/>, computed the way the game computes
    /// it on save — via <see cref="ZPackage.GenerateHash"/> over the payload
    /// package. This is what a writer emits; <see cref="Hash"/> is what the file
    /// happened to contain.
    /// </summary>
    public byte[] ComputeHash()
    {
        return new ZPackage(PlayerData).GenerateHash();
    }
}
