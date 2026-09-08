namespace Norn.GameCore.Primitives;

// mirrors: Vector2i
// source:  Valheim 0.221.10 (assembly_utils) — the type is absent from the
//          0.221.4 tree, which ships no assembly_utils at all. Low-churn helper,
//          but this is an unverified-for-0.221.4 mirror.
// note:    Only the members the .fch path needs are carried: fields, zero, and
//          equality. The game's arithmetic operators, Magnitude, Distance,
//          ToVector2, and the Vector2/Vector3 truncating constructors are game
//          runtime concerns with no bearing on the byte format.
// note:    The game does NOT override Equals(object); boxed comparison falls
//          through to ValueType.Equals. Overridden here so the round-trip
//          oracle behaves the same boxed or unboxed.

/// <summary>Two 32-bit integers. 8 bytes on the wire, in x, y order.</summary>
public struct Vector2i : IEquatable<Vector2i>
{
    public int x;
    public int y;

    public Vector2i(int _x, int _y)
    {
        x = _x;
        y = _y;
    }

    public static Vector2i zero => new Vector2i(0, 0);

    public bool Equals(Vector2i other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is Vector2i other && Equals(other);
    }

    // mirrors the game's GetHashCode exactly: x.GetHashCode() ^ y.GetHashCode().
    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode();
    }

    public static bool operator ==(Vector2i a, Vector2i b) => a.Equals(b);

    public static bool operator !=(Vector2i a, Vector2i b) => !a.Equals(b);

    // mirrors the game's ToString: "x,y", no brackets, no space.
    public override string ToString()
    {
        return x + "," + y;
    }
}
