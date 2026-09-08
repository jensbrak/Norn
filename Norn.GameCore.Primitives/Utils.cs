using System.IO.Compression;

namespace Norn.GameCore.Primitives;

// mirrors: Utils.Compress(byte[]) / Utils.Decompress(byte[])
// source:  Valheim 0.221.10 (assembly_utils) — absent from the 0.221.4 tree.
// note:    Only the two compression helpers are carried. The rest of the game's
//          Utils is Unity runtime glue.

/// <summary>
/// The gzip helpers behind <see cref="ZPackage.WriteCompressed"/> and
/// <see cref="ZPackage.ReadCompressedPackage"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Compression is not byte-reproducible, and this is the single largest R1
/// hazard in the format.</b> The output of <see cref="Compress"/> depends on the
/// deflate implementation, on how <see cref="CompressionLevel.Fastest"/> maps to
/// deflate parameters, and on the gzip header's OS and MTIME fields. The game
/// runs on Mono; we do not. Recompressing a blob we decompressed will not
/// reproduce the original bytes, and any round trip routed through
/// <see cref="Compress"/> fails for reasons unrelated to the parser.
/// </para>
/// <para>
/// Therefore: a compressed region is held as an opaque <c>byte[]</c> and
/// re-emitted verbatim (rule R3) by default. <see cref="Decompress"/> is for
/// reading a region's contents; <see cref="Compress"/> must never appear on a
/// write path that R1 covers <b>except</b> the one narrow, explicit exception
/// carved out for <c>Minimap.Encode</c> (<c>Norn.GameCore</c>), called only from <c>CharacterEditor.ExploreAllMap</c>
/// (<c>Norn.Adapter</c>) for a single world's <c>WorldPlayerData.m_mapData</c>
/// the user explicitly chose to edit. That path never claims R1 — only R2
/// (this project's own decode-then-encode round trip) — and every other
/// world's map blob, and every unedited save, stays exactly the opaque
/// pass-through this rule describes. Do not add a second call site to
/// <see cref="Compress"/> without a deliberate, recorded decision.
/// </para>
/// </remarks>
public static class Utils
{
    public static byte[] Compress(byte[] inputArray)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest))
            {
                gzipStream.Write(inputArray, 0, inputArray.Length);
            }

            return memoryStream.ToArray();
        }
    }

    public static byte[] Decompress(byte[] inputArray)
    {
        using (MemoryStream memoryStream = new MemoryStream(inputArray))
        {
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                using (MemoryStream memoryStream2 = new MemoryStream())
                {
                    gzipStream.CopyTo(memoryStream2);
                    return memoryStream2.ToArray();
                }
            }
        }
    }
}
