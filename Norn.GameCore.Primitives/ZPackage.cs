using System.Security.Cryptography;

namespace Norn.GameCore.Primitives;

// mirrors: ZPackage
// source:  Valheim 0.221.10
// note:    The game declares this in the global namespace; ours is namespaced.
//          Unavoidable, and the only such divergence in this project.
// note:    SetReader(BinaryReader) and SetWriter(BinaryWriter) are NOT carried.
//          Both replace the reader/writer with one pointing at a foreign stream,
//          after which GetPos/SetPos/Size/GetArray no longer describe this
//          instance. Their only two call sites are in ZDOMan's world .db
//          save/load path, unreachable from the .fch path. Carrying them would
//          mean exporting a
//          way to break the class's own invariant for no gain.
// note:    Field order follows the decompilation, which emits fields last. That
//          is not idiomatic C#, and it is deliberate — it keeps this file
//          diffable top-to-bottom against a fresh decompile.
// note:    No length guards are added anywhere. A corrupt Int32 length prefix
//          reaches BinaryReader.ReadBytes and attempts an allocation of that
//          size, exactly as it does in the game. Adding a bound would be a
//          divergence; the decision to add one belongs to the layer that opens
//          untrusted files, not here.

/// <summary>
/// The game's serialization buffer: a <see cref="MemoryStream"/> with a
/// <see cref="BinaryReader"/> and a <see cref="BinaryWriter"/> over it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reader and the writer share one stream, and therefore one position.</b>
/// Writing after reading continues at the read cursor and overwrites in place —
/// it does not append. <see cref="Load"/> and <see cref="Clear"/> are the only
/// resets. This surface deliberately does not hide that.
/// </para>
/// <para>
/// <b><see cref="BinaryReader.ReadBytes"/> short-returns at end of stream rather
/// than throwing.</b> Every byte-array read here inherits that: a truncated file
/// yields a short array silently, where a scalar read would have thrown
/// <see cref="EndOfStreamException"/>. Parsers above this layer must not treat a
/// successful byte-array read as proof the bytes were there.
/// </para>
/// <para>
/// Both the reader and the writer are constructed with the single-argument
/// stream overloads, exactly as the game does, so string encoding is whatever
/// the host runtime defaults to. Passing an explicit encoding would be a
/// divergence — do not "helpfully" add one.
/// </para>
/// </remarks>
public class ZPackage
{
    public ZPackage()
    {
        m_writer = new BinaryWriter(m_stream);
        m_reader = new BinaryReader(m_stream);
    }

    public ZPackage(string base64String)
    {
        m_writer = new BinaryWriter(m_stream);
        m_reader = new BinaryReader(m_stream);
        if (string.IsNullOrEmpty(base64String))
        {
            return;
        }

        byte[] array = Convert.FromBase64String(base64String);
        m_stream.Write(array, 0, array.Length);
        m_stream.Position = 0L;
    }

    public ZPackage(byte[] data)
    {
        m_writer = new BinaryWriter(m_stream);
        m_reader = new BinaryReader(m_stream);
        m_stream.Write(data, 0, data.Length);
        m_stream.Position = 0L;
    }

    public ZPackage(byte[] data, int dataSize)
    {
        m_writer = new BinaryWriter(m_stream);
        m_reader = new BinaryReader(m_stream);
        m_stream.Write(data, 0, dataSize);
        m_stream.Position = 0L;
    }

    public void Load(byte[] data)
    {
        Clear();
        m_stream.Write(data, 0, data.Length);
        m_stream.Position = 0L;
    }

    public void Write(ZPackage pkg)
    {
        byte[] array = pkg.GetArray();
        m_writer.Write(array.Length);
        m_writer.Write(array);
    }

    public void WriteCompressed(ZPackage pkg)
    {
        byte[] array = Utils.Compress(pkg.GetArray());
        m_writer.Write(array.Length);
        m_writer.Write(array);
    }

    public void Write(byte[] array)
    {
        m_writer.Write(array.Length);
        m_writer.Write(array);
    }

    public void Write(byte data)
    {
        m_writer.Write(data);
    }

    public void Write(sbyte data)
    {
        m_writer.Write(data);
    }

    public void Write(char data)
    {
        m_writer.Write(data);
    }

    public void Write(bool data)
    {
        m_writer.Write(data);
    }

    public void Write(int data)
    {
        m_writer.Write(data);
    }

    public void Write(uint data)
    {
        m_writer.Write(data);
    }

    public void Write(short data)
    {
        m_writer.Write(data);
    }

    public void Write(ushort data)
    {
        m_writer.Write(data);
    }

    public void Write(long data)
    {
        m_writer.Write(data);
    }

    public void Write(ulong data)
    {
        m_writer.Write(data);
    }

    public void Write(float data)
    {
        m_writer.Write(data);
    }

    public void Write(double data)
    {
        m_writer.Write(data);
    }

    public void Write(string data)
    {
        m_writer.Write(data);
    }

    public void Write(ZDOID id)
    {
        m_writer.Write(id.UserID);
        m_writer.Write(id.ID);
    }

    public void Write(Vector3 v3)
    {
        m_writer.Write(v3.x);
        m_writer.Write(v3.y);
        m_writer.Write(v3.z);
    }

    public void Write(Vector2i v2)
    {
        m_writer.Write(v2.x);
        m_writer.Write(v2.y);
    }

    public void Write(Vector2s v2)
    {
        m_writer.Write(v2.x);
        m_writer.Write(v2.y);
    }

    public void Write(Quaternion q)
    {
        m_writer.Write(q.x);
        m_writer.Write(q.y);
        m_writer.Write(q.z);
        m_writer.Write(q.w);
    }

    // World-format only, introduced at world version 33. Not on the .fch path.
    // One byte below 128; otherwise two, with the high bit of the first byte as
    // the "two-byte" flag. No range guard in the game, and none added here: a
    // negative value takes the one-byte branch and truncates, and a value at or
    // above 32768 overflows the first cast.
    public void WriteNumItems(int numItems)
    {
        if (numItems < 128)
        {
            m_writer.Write((byte)numItems);
            return;
        }

        m_writer.Write((byte)((numItems >> 8) | 128));
        m_writer.Write((byte)numItems);
    }

    public ZDOID ReadZDOID()
    {
        // Argument evaluation is left to right, so the Int64 is consumed first.
        return new ZDOID(m_reader.ReadInt64(), m_reader.ReadUInt32());
    }

    public bool ReadBool()
    {
        return m_reader.ReadBoolean();
    }

    public char ReadChar()
    {
        return m_reader.ReadChar();
    }

    public byte ReadByte()
    {
        return m_reader.ReadByte();
    }

    public int ReadNumItems()
    {
        byte b = m_reader.ReadByte();
        if ((b & 128) != 0)
        {
            return ((b & 127) << 8) | m_reader.ReadByte();
        }

        return b;
    }

    public sbyte ReadSByte()
    {
        return m_reader.ReadSByte();
    }

    public short ReadShort()
    {
        return m_reader.ReadInt16();
    }

    public ushort ReadUShort()
    {
        return m_reader.ReadUInt16();
    }

    public int ReadInt()
    {
        return m_reader.ReadInt32();
    }

    public uint ReadUInt()
    {
        return m_reader.ReadUInt32();
    }

    public long ReadLong()
    {
        return m_reader.ReadInt64();
    }

    public ulong ReadULong()
    {
        return m_reader.ReadUInt64();
    }

    public float ReadSingle()
    {
        return m_reader.ReadSingle();
    }

    public double ReadDouble()
    {
        return m_reader.ReadDouble();
    }

    public string ReadString()
    {
        return m_reader.ReadString();
    }

    public Vector3 ReadVector3()
    {
        // Object-initializer assignments evaluate in source order, so the game's
        // read order is x, y, z. Kept as separate assignments rather than a
        // constructor call for that reason — a constructor would evaluate its
        // arguments in the same order, but the equivalence would be an inference
        // rather than something visible in the diff.
        return new Vector3
        {
            x = m_reader.ReadSingle(),
            y = m_reader.ReadSingle(),
            z = m_reader.ReadSingle()
        };
    }

    public Vector2i ReadVector2i()
    {
        return new Vector2i
        {
            x = m_reader.ReadInt32(),
            y = m_reader.ReadInt32()
        };
    }

    public Vector2s ReadVector2s()
    {
        return new Vector2s
        {
            x = m_reader.ReadInt16(),
            y = m_reader.ReadInt16()
        };
    }

    public Quaternion ReadQuaternion()
    {
        return new Quaternion
        {
            x = m_reader.ReadSingle(),
            y = m_reader.ReadSingle(),
            z = m_reader.ReadSingle(),
            w = m_reader.ReadSingle()
        };
    }

    public ZPackage ReadCompressedPackage()
    {
        int count = m_reader.ReadInt32();
        byte[] inputArray = m_reader.ReadBytes(count);
        return new ZPackage(Utils.Decompress(inputArray));
    }

    public ZPackage ReadPackage()
    {
        int count = m_reader.ReadInt32();
        byte[] data = m_reader.ReadBytes(count);
        return new ZPackage(data);
    }

    // The ref is vestigial in the game too — the method mutates the instance and
    // never assigns to the parameter. Kept as written.
    public void ReadPackage(ref ZPackage pkg)
    {
        int count = m_reader.ReadInt32();
        byte[] array = m_reader.ReadBytes(count);
        pkg.Clear();
        pkg.m_stream.Write(array, 0, array.Length);
        pkg.m_stream.Position = 0L;
    }

    /// <summary>Length-prefixed: reads an <c>Int32</c> count, then that many bytes.</summary>
    public byte[] ReadByteArray()
    {
        int count = m_reader.ReadInt32();
        return m_reader.ReadBytes(count);
    }

    /// <summary>
    /// Raw: reads exactly <paramref name="num"/> bytes with no prefix. The caller
    /// supplies the count from elsewhere — on the <c>.fch</c> path, from the map
    /// texture dimensions.
    /// </summary>
    public byte[] ReadByteArray(int num)
    {
        return m_reader.ReadBytes(num);
    }

    public string GetBase64()
    {
        return Convert.ToBase64String(GetArray());
    }

    /// <summary>
    /// The whole buffer from offset 0 to length, independent of the current
    /// position. Allocates a fresh copy on every call, as the game does — callers
    /// that invoke it twice in a row are mirrored, not optimised.
    /// </summary>
    public byte[] GetArray()
    {
        m_writer.Flush();
        m_stream.Flush();
        return m_stream.ToArray();
    }

    public void SetPos(int pos)
    {
        m_stream.Position = pos;
    }

    public int GetPos()
    {
        return (int)m_stream.Position;
    }

    /// <summary>
    /// Total length, <b>not</b> bytes remaining. The game exposes no
    /// remaining/EOF accessor; a parser that wants one compares
    /// <see cref="GetPos"/> against this.
    /// </summary>
    public int Size()
    {
        m_writer.Flush();
        m_stream.Flush();
        return (int)m_stream.Length;
    }

    // Flushes the writer but not the stream, unlike GetArray and Size. With a
    // MemoryStream that is a distinction without a difference; kept for the diff.
    public void Clear()
    {
        m_writer.Flush();
        m_stream.SetLength(0L);
        m_stream.Position = 0L;
    }

    /// <summary>SHA-512 over the entire package contents from offset 0. 64 bytes.</summary>
    public byte[] GenerateHash()
    {
        byte[] array = GetArray();
        return SHA512.Create().ComputeHash(array);
    }

    private MemoryStream m_stream = new MemoryStream();

    private BinaryWriter m_writer;

    private BinaryReader m_reader;
}
