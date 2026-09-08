namespace Norn.GameCore.Primitives;

// mirrors: Vector2s
// source:  Valheim 0.221.10 (assembly_utils) — absent from the 0.221.4 tree.
// note:    Not reachable from the .fch load path. Carried only because ZPackage
//          exposes Write(Vector2s)/ReadVector2s and Primitives mirrors that
//          surface whole.
// note:    The game's cross-type comparison operators against Vector2i are NOT
//          reproduced. Its operator!=(Vector2s, Vector2i) has the body of
//          operator==, so it returns true for equal operands — a real defect in
//          the game, not a decompilation artefact. Reproducing a bug in a type
//          nothing here calls would be liability with no isomorphism benefit;
//          the operators are omitted entirely rather than silently corrected.

/// <summary>Two 16-bit integers. 4 bytes on the wire, in x, y order.</summary>
public struct Vector2s : IEquatable<Vector2s>
{
    public short x;
    public short y;

    public Vector2s(short x, short y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2s zero => new Vector2s(0, 0);

    public bool Equals(Vector2s other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is Vector2s other && Equals(other);
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode();
    }

    public static bool operator ==(Vector2s a, Vector2s b) => a.Equals(b);

    public static bool operator !=(Vector2s a, Vector2s b) => !a.Equals(b);

    public override string ToString()
    {
        return x + "," + y;
    }
}
