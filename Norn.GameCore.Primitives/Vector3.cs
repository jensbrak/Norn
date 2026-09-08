namespace Norn.GameCore.Primitives;

// mirrors: UnityEngine.Vector3 (wire behaviour only)
// source:  Valheim 0.221.10
// note:    Unity type; not present in the decompiled tree. Only the field order
//          on the wire (x, y, z as three little-endian float32) is derivable from
//          source, via ZPackage.Write(Vector3)/ReadVector3. Everything else here
//          is ours.
// note:    Equality is EXACT, field for field. Unity's Vector3.operator== is an
//          approximate comparison — squared distance below ~1e-10 counts as
//          equal — which would quietly pass an R2 deep-equal on values that are
//          not bit-identical. Approximate equality is wrong for a round-trip
//          oracle, so it is not reproduced.

/// <summary>Three single-precision floats. 12 bytes on the wire, in x, y, z order.</summary>
public struct Vector3 : IEquatable<Vector3>
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3 zero => new Vector3(0f, 0f, 0f);

    public static Vector3 one => new Vector3(1f, 1f, 1f);

    /// <summary>
    /// Bit-exact equality, including NaN payloads and the sign of zero. Uses
    /// <see cref="float.Equals(float)"/> rather than <c>==</c> so that NaN equals
    /// NaN — a round trip that preserves a NaN's bits must compare equal.
    /// </summary>
    public bool Equals(Vector3 other)
    {
        return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);
    }

    public override bool Equals(object obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, z);
    }

    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);

    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
    }
}
