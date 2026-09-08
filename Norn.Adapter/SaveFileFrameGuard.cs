using System.Buffers.Binary;

namespace Norn.Adapter;

/// <summary>
/// A structural pre-check at the file-opening boundary, run before a path is
/// handed to the mirror.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Norn.GameCore.Primitives.ZPackage"/>'s own header note states
/// that it adds no length guards anywhere, deliberately — "the decision to
/// add one belongs to the layer that opens untrusted files, not here." This
/// is that layer.
/// </para>
/// <para>
/// The specific hazard (found in review): the file frame opens with an
/// <c>Int32</c> payload length that is then passed straight to
/// <c>BinaryReader.ReadBytes</c>, which allocates the requested count
/// <i>before</i> discovering end-of-stream. A four-byte file declaring a
/// two-billion-byte payload therefore drives a two-gigabyte allocation
/// attempt on open. Comparing the declared length against the bytes the file
/// actually has closes that without bounding anything a real save could
/// contain: a valid frame is
/// <c>[int payloadLength][payload][int hashLength][hash][trailing]</c>, so a
/// legitimate <c>payloadLength</c> is always within the remaining file.
/// </para>
/// </remarks>
internal static class SaveFileFrameGuard
{
    /// <summary>
    /// Throws <see cref="InvalidDataException"/> when <paramref name="path"/>
    /// cannot be a save-file frame at all. Throwing rather than returning a
    /// status deliberately: it matches the loud-failure-on-corrupt-input
    /// stance <c>Minimap.Decode</c>'s own RISK note already establishes for
    /// this project, and it reaches the UI as a specific "failed to open"
    /// message instead of being flattened into the version-range refusal
    /// that a plain <c>null</c> return would render as.
    /// </summary>
    internal static void ThrowIfFrameImplausible(string path)
    {
        using var stream = File.OpenRead(path);

        if (stream.Length < sizeof(int))
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} is too short to be a save file ({stream.Length} bytes).");
        }

        Span<byte> lengthPrefix = stackalloc byte[sizeof(int)];
        stream.ReadExactly(lengthPrefix);
        var declared = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);

        if (declared < 0 || declared > stream.Length - sizeof(int))
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} declares a {declared}-byte payload but only has "
                + $"{stream.Length - sizeof(int)} bytes after its length prefix; the file is truncated or not a save file.");
        }
    }
}
