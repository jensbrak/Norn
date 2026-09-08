namespace Norn.GameCore.Primitives;

// mirrors: UnityEngine.Quaternion (wire behaviour only)
// source:  Valheim 0.221.10
// note:    Unity type; not present in the decompiled tree. Only the wire field
//          order (x, y, z, w as four little-endian float32) is derivable, via
//          ZPackage.Write(Quaternion)/ReadQuaternion.
// note:    Exact field equality, for the reason given in Vector3.cs.
// note:    Not reachable from the .fch load path — carried because ZPackage
//          exposes the read/write pair and Primitives mirrors that surface whole.

/// <summary>Four single-precision floats. 16 bytes on the wire, in x, y, z, w order.</summary>
public struct Quaternion : IEquatable<Quaternion>
{
    public float x;
    public float y;
    public float z;
    public float w;

    public Quaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

    public bool Equals(Quaternion other)
    {
        return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z) && w.Equals(other.w);
    }

    public override bool Equals(object obj)
    {
        return obj is Quaternion other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, z, w);
    }

    public static bool operator ==(Quaternion a, Quaternion b) => a.Equals(b);

    public static bool operator !=(Quaternion a, Quaternion b) => !a.Equals(b);

    public override string ToString()
    {
        return $"({x}, {y}, {z}, {w})";
    }
}
