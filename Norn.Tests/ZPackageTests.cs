using Norn.GameCore.Primitives;

namespace Norn.Tests;

/// <summary>
/// Pins <see cref="ZPackage"/>'s wire encoding and the behaviours a parser built
/// on it has to know about.
/// </summary>
/// <remarks>
/// The corpus only exercises the envelope at L0, so it would not catch a wrong
/// field order inside <c>ReadVector3</c> or a missing length prefix — those go
/// wrong for the first time deep inside a payload, where a byte
/// offset is all the diagnostic you get. Cheaper to pin them here.
/// </remarks>
public class ZPackageTests
{
    [Fact]
    public void Scalars_round_trip_through_a_shared_position()
    {
        var package = new ZPackage();

        package.Write(true);
        package.Write((byte)0x7F);
        package.Write((short)-2);
        package.Write(1234567);
        package.Write(-9876543210L);
        package.Write(1.5f);
        package.Write("hej hej");

        // The reader and the writer share one stream and therefore one position.
        // Rewinding is the caller's job; nothing in the class does it for you.
        package.SetPos(0);

        Assert.True(package.ReadBool());
        Assert.Equal((byte)0x7F, package.ReadByte());
        Assert.Equal((short)-2, package.ReadShort());
        Assert.Equal(1234567, package.ReadInt());
        Assert.Equal(-9876543210L, package.ReadLong());
        Assert.Equal(1.5f, package.ReadSingle());
        Assert.Equal("hej hej", package.ReadString());
    }

    [Fact]
    public void Ints_are_little_endian()
    {
        var package = new ZPackage();
        package.Write(0x01020304);

        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, package.GetArray());
    }

    [Fact]
    public void Strings_are_length_prefixed_utf8_counting_bytes_not_characters()
    {
        var package = new ZPackage();
        // Three characters, four UTF-8 bytes: the prefix must say 4.
        package.Write("nör");

        var bytes = package.GetArray();

        Assert.Equal(4, bytes[0]);
        Assert.Equal(5, bytes.Length);
    }

    [Fact]
    public void Vector3_is_three_floats_in_x_y_z_order()
    {
        var package = new ZPackage();
        package.Write(new Vector3(1f, 2f, 3f));

        var expected = BitConverter.GetBytes(1f)
            .Concat(BitConverter.GetBytes(2f))
            .Concat(BitConverter.GetBytes(3f))
            .ToArray();

        Assert.Equal(expected, package.GetArray());

        package.SetPos(0);
        Assert.Equal(new Vector3(1f, 2f, 3f), package.ReadVector3());
    }

    [Fact]
    public void Vector2i_is_two_ints_in_x_y_order()
    {
        var package = new ZPackage();
        package.Write(new Vector2i(3, 7));

        Assert.Equal(8, package.GetArray().Length);

        package.SetPos(0);
        Assert.Equal(new Vector2i(3, 7), package.ReadVector2i());
    }

    [Fact]
    public void Quaternion_is_four_floats_in_x_y_z_w_order()
    {
        var package = new ZPackage();
        package.Write(new Quaternion(1f, 2f, 3f, 4f));

        var expected = BitConverter.GetBytes(1f)
            .Concat(BitConverter.GetBytes(2f))
            .Concat(BitConverter.GetBytes(3f))
            .Concat(BitConverter.GetBytes(4f))
            .ToArray();

        Assert.Equal(expected, package.GetArray());
    }

    [Fact]
    public void ZDOID_is_an_int64_then_a_uint32()
    {
        var package = new ZPackage();
        package.Write(new ZDOID(-5L, 9u));

        Assert.Equal(12, package.GetArray().Length);

        package.SetPos(0);
        var id = package.ReadZDOID();

        Assert.Equal(-5L, id.UserID);
        Assert.Equal(9u, id.ID);
    }

    [Fact]
    public void Vector3_equality_is_exact_not_approximate()
    {
        // Unity's Vector3.operator== treats a squared distance below ~1e-10 as
        // equal. Under that rule these two compare equal, and an R2 deep-equal
        // would pass on values that are not bit-identical.
        Assert.NotEqual(new Vector3(0f, 0f, 0f), new Vector3(1e-7f, 0f, 0f));
    }

    [Fact]
    public void Vector3_equality_treats_matching_NaN_bits_as_equal()
    {
        // A round trip that preserves a NaN's bits must compare equal, so equality
        // goes through float.Equals rather than ==.
        var nan = new Vector3(float.NaN, 0f, 0f);

        Assert.Equal(nan, new Vector3(float.NaN, 0f, 0f));
    }

    [Fact]
    public void Length_prefixed_and_raw_byte_array_reads_differ()
    {
        var package = new ZPackage();
        package.Write(new byte[] { 1, 2, 3 });
        package.SetPos(0);

        // Self-describing: consumes the Int32 prefix, then that many bytes.
        Assert.Equal(new byte[] { 1, 2, 3 }, package.ReadByteArray());

        // Raw: no prefix, count supplied by the caller. Reading the same buffer
        // this way sees the prefix bytes as data.
        package.SetPos(0);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, package.ReadByteArray(4));
    }

    [Fact]
    public void Nested_packages_are_length_prefixed_and_read_back_whole()
    {
        var inner = new ZPackage();
        inner.Write(42);
        inner.Write("inner");

        var outer = new ZPackage();
        outer.Write(7);
        outer.Write(inner);
        outer.Write(8);

        outer.SetPos(0);

        Assert.Equal(7, outer.ReadInt());

        var readBack = outer.ReadPackage();
        Assert.Equal(42, readBack.ReadInt());
        Assert.Equal("inner", readBack.ReadString());

        // The outer position must land exactly past the nested region — position
        // arithmetic across a nested boundary is what this guards against.
        Assert.Equal(8, outer.ReadInt());
    }

    [Fact]
    public void GetArray_returns_the_whole_buffer_regardless_of_position()
    {
        var package = new ZPackage();
        package.Write(1);
        package.Write(2);
        package.SetPos(4);

        Assert.Equal(8, package.GetArray().Length);
    }

    [Fact]
    public void Size_is_total_length_not_bytes_remaining()
    {
        var package = new ZPackage();
        package.Write(1);
        package.Write(2);
        package.SetPos(4);

        Assert.Equal(8, package.Size());
        Assert.Equal(4, package.GetPos());
    }

    [Fact]
    public void Clear_empties_the_buffer_and_rewinds()
    {
        var package = new ZPackage();
        package.Write(1);
        package.Clear();

        Assert.Equal(0, package.Size());
        Assert.Equal(0, package.GetPos());
    }

    [Fact]
    public void Load_replaces_the_contents_and_rewinds()
    {
        var package = new ZPackage();
        package.Write(999);
        package.Load(BitConverter.GetBytes(5));

        Assert.Equal(4, package.Size());
        Assert.Equal(5, package.ReadInt());
    }

    [Fact]
    public void Constructing_from_bytes_leaves_the_position_at_zero()
    {
        var package = new ZPackage(BitConverter.GetBytes(11));

        Assert.Equal(0, package.GetPos());
        Assert.Equal(11, package.ReadInt());
    }

    [Fact]
    public void GenerateHash_is_SHA512_over_the_whole_buffer_from_offset_zero()
    {
        var package = new ZPackage();
        package.Write(1234);
        package.SetPos(2);

        var expected = System.Security.Cryptography.SHA512.HashData(package.GetArray());

        Assert.Equal(expected, package.GenerateHash());
        Assert.Equal(64, package.GenerateHash().Length);
    }

    [Fact]
    public void Byte_array_reads_short_return_at_end_of_stream_instead_of_throwing()
    {
        // The asymmetry that makes a truncated file look like a clean parse:
        // scalar reads throw, byte-array reads return what is left. Anything
        // built on this must check lengths itself.
        var package = new ZPackage(new byte[] { 1, 2, 3 });

        Assert.Equal(3, package.ReadByteArray(100).Length);

        package.SetPos(0);
        package.ReadByteArray(3);
        Assert.Throws<EndOfStreamException>(() => package.ReadInt());
    }

    [Fact]
    public void Writing_after_reading_overwrites_in_place_rather_than_appending()
    {
        var package = new ZPackage();
        package.Write(1);
        package.Write(2);
        package.SetPos(0);
        package.ReadInt();

        // Position is 4, so this lands on top of the second int.
        package.Write(3);

        Assert.Equal(8, package.Size());
        package.SetPos(4);
        Assert.Equal(3, package.ReadInt());
    }
}
