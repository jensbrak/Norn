namespace Norn.GameCore.Primitives;

// mirrors: ZDOID
// source:  Valheim 0.221.10
// note:    STRUCTURAL DIVERGENCE, deliberate. The game stores the 64-bit user ID
//          interned in a process-global List<long> and keeps only a 16-bit key
//          in the struct (UserKey), resolving it back through a static lookup.
//          That table is mutable global state that exists to shrink live network
//          objects; it has no bearing on the byte format, and reproducing it in
//          a standalone reader would mean a shared mutable static that two
//          concurrently-open save files would corrupt for each other.
//          Modelled here as the wire shape it actually is: (long UserID, uint ID).
// note:    Consequently the game's Reset/AddUser/GetUserID statics, the
//          BinaryReader constructor, SetID, and CompareTo are not carried.
//          Equality here compares UserID rather than the game's UserKey — the
//          same relation whenever the intern table is 1:1, which it always is
//          for values read off disk.
// note:    Reachable from Player.Load only under the player-data version == 2
//          branch, where the value is read and discarded. No corpus file is
//          anywhere near that version.

/// <summary>
/// A ZDO identity as it appears on the wire: an <c>int64</c> user ID followed by
/// a <c>uint32</c> object ID. 12 bytes.
/// </summary>
public struct ZDOID : IEquatable<ZDOID>
{
    public long UserID { get; private set; }

    public uint ID { get; private set; }

    public ZDOID(long userID, uint id)
    {
        UserID = userID;
        ID = id;
    }

    public static ZDOID None => new ZDOID(0L, 0u);

    public bool IsNone()
    {
        return UserID == 0L && ID == 0u;
    }

    public bool Equals(ZDOID other)
    {
        return UserID == other.UserID && ID == other.ID;
    }

    public override bool Equals(object obj)
    {
        return obj is ZDOID other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserID, ID);
    }

    public static bool operator ==(ZDOID a, ZDOID b) => a.Equals(b);

    public static bool operator !=(ZDOID a, ZDOID b) => !a.Equals(b);

    public override string ToString()
    {
        return UserID + ":" + ID;
    }
}
