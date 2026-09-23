using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: World field declarations (identity-relevant subset) +
//          World.LoadWorld(SaveWithBackups)
// source:  Valheim 1.0.15
// note:    NOT AN EXHAUSTIVE FIELD LIST. Only fields reachable from
//          LoadWorld's read path are carried, per the same rule
//          PlayerProfile.cs already applies to its own load path. Not
//          carried: m_worldName (not itself serialized — set from the
//          enumerated save's own filename stem; Norn's UI layer supplies a
//          path separately, see WorldFileLocator), m_menu,
//          m_createBackupBeforeSaving, m_startingKeysChanged, saves,
//          m_dataError, m_fileSource, and — new at 1.0.7 — m_chunkedSave,
//          m_saveNumber and m_biomeData (all runtime-only or filesystem
//          choreography, no bearing on wire bytes — same reasoning already
//          applied to PlayerProfile's own omissions of this kind). The
//          0.221.10 mirror named a field m_fileName here; no such field
//          exists at 1.0.7.
// note:    READ-ONLY MIRROR. Norn never writes a .fwl file — no
//          SaveWorldMetaData counterpart exists or is planned. This is
//          enrichment data for the Worlds tab, not part of any round-trip
//          harness; R1/R2/R3 do not apply here the way they do to
//          PlayerProfile's Load/Save pair.
// note:    OMISSION, accepted — but with a real behavioural consequence,
//          not just a byte-safe skip. CheckDbFile() (sets m_dataError from a
//          filesystem existence check on the paired .db2, consumes no bytes)
//          has no counterpart — the .db2 file is confirmed irrelevant to this
//          feature, and GameCore has
//          no other reason to touch it. Source's callers (World.cs:158 and 180)
//          treat any non-None SaveDataError, MissingDB included, as a failed
//          load; this mirror has no SaveDataError at all (see the SIGNATURE
//          DIVERGENCE note on Load below), so Norn will resolve a name/seed
//          from a .fwl2 whose paired .db2 is missing — a world the game itself
//          would refuse to open. Deliberate: a missing .db has no bearing on
//          whether the .fwl's own identity fields are readable, and this
//          mirror never touches .db2 at all either way.
//          `world != Version.World.DeepNorth -> m_createBackupBeforeSaving
//          = true` (also no bytes, and a literal `v != 37` at 0.221.10) is
//          likewise not carried, same "filesystem choreography, not format"
//          reasoning PlayerProfile.cs already documents for its own
//          equivalent field — but see the note at its actual position in
//          Load below (a version-literal omission noted only up here, far
//          from where it would matter on a patch-day diff, is easy to miss;
//          PlayerProfile.cs's own
//          equivalent version-comparison omission is flagged at its position
//          for exactly this reason).
// note:    VISIBILITY. Fields private in source are made public here, same
//          reasoning as PlayerProfile's own VISIBILITY note — Adapter needs
//          direct access.
// note:    NAMING. Distinct from Norn.Adapter's WorldDto/WorldsDto/WorldMapDto,
//          which describe a *character's* recorded visit to a world
//          (PlayerProfile.m_worldData / PlayerProfile.WorldPlayerData). This
//          class mirrors a different source class entirely (World, not
//          PlayerProfile.WorldPlayerData) — a world's own identity, read
//          from its own .fwl file.
// note:    FIELD ORDER. Declared in source's own declaration order
//          (World.cs:391-446), which differs from Load's read order below —
//          same "declaration order, not read order" convention
//          PlayerProfile.cs's own field-declaration note already states
//          explicitly for its fields.
public class World
{
    public string m_name = "";

    public string m_seedName = "";

    public int m_seed;

    public long m_uid;

    public List<string> m_startingGlobalKeys = new List<string>();

    public int m_worldGenVersion;

    public bool m_needsDB;

    // note: Set identically to source (`m_worldVersion = v`) at this exact
    // point in LoadWorld's read sequence — see Load below. Kept for parity
    // with PlayerProfile.ProfileVersion: useful for tests/diagnostics, zero
    // bearing on any other field's bytes.
    // note: TYPE DIVERGENCE. Version.World in source as of 1.0.7 (it was a
    // plain int through 0.221.10, when the version constants were loose
    // const ints rather than enum members). Held as int here so Adapter and
    // the tests keep a bare number to display and compare, matching how
    // PlayerProfile.ProfileVersion is already handled. The read itself is
    // done through the enum — see Load.
    // note: Declared at source's own position at 1.0.7, after m_needsDB
    // rather than before it, matching source's own field order —
    // m_playerHistory below is declared immediately after this field in
    // source, and the two only line up in a side-by-side diff if this one
    // is in the right place.
    public int m_worldVersion;

    // note: NEW AT 1.0.7. Only read when world version >= 41 — see Load.
    public List<ZNet.CrossNetworkUserInfo> m_playerHistory = new List<ZNet.CrossNetworkUserInfo>();

    // mirrors: World.LoadWorld(SaveWithBackups)
    // source:  Valheim 1.0.15
    // note:    SIGNATURE DIVERGENCE. static World LoadWorld(SaveWithBackups)
    //          in source, resolving its own file paths from a
    //          SaveWithBackups value Norn has no counterpart for
    //          (SaveSystem's enumeration machinery — file discovery is
    //          Norn.UI's job, same reasoning already
    //          applied to PlayerProfile's own path handling). Takes the
    //          already-opened payload bytes directly instead, same shape as
    //          PlayerProfile.LoadPlayerDataFromDisk's separation of frame
    //          reading from payload parsing.
    // note:    BEHAVIOURAL DIVERGENCE. Source wraps the whole read in a
    //          try/catch that swallows any exception, returning a stub
    //          World with a data-error flag. Exceptions propagate here
    //          instead, same divergence PlayerProfile.LoadPlayerFromDisk
    //          already documents. Version incompatibility is not an
    //          exception in source either way (an explicit early return with
    //          a stub, reading nothing further) and stays that way here:
    //          Load returns false, mirroring PlayerProfile.Load's exact
    //          shape.
    // note:    NO SaveDataError. Source's stub World also carries a
    //          SaveDataError (BadVersion, MissingDB, ...) its callers gate
    //          on (World.cs:158 and 180); this mirror has no such type, so a
    //          caller here cannot distinguish "loaded cleanly" from "loaded
    //          despite a missing .db" — see the OMISSION note on CheckDbFile
    //          in the field-declaration header above for why that specific
    //          case is deliberate, not just unhandled.
    // note:    INSTANCE REUSE. Source always Loads into a freshly
    //          `new World()` (World.cs:297) — a second Load on the same
    //          instance has no source behavior to mirror at all. Every
    //          current call site already constructs fresh
    //          (WorldIdentityCatalog.cs, WorldTests.cs, WorldCorpusTests.cs),
    //          so this is latent, not live — m_startingGlobalKeys.Clear()
    //          below exists only so a hypothetical reused instance can't
    //          silently accumulate duplicate keys across calls; every other
    //          field either has no cross-call state (scalars, always
    //          reassigned) or gets no source-mandated reset either way.
    public bool Load(byte[] payload)
    {
        ZPackage zpackage = new ZPackage(payload);
        m_startingGlobalKeys.Clear();

        Version.World ver = (Version.World)zpackage.ReadInt();
        if (!Version.IsWorldVersionCompatible(ver))
        {
            return false;
        }

        m_name = zpackage.ReadString();
        m_seedName = zpackage.ReadString();
        m_seed = zpackage.ReadInt();
        m_uid = zpackage.ReadLong();
        m_worldVersion = (int)ver;

        if (ver >= Version.World.WorldGenVersion)
        {
            m_worldGenVersion = zpackage.ReadInt();
        }

        m_needsDB = ver >= Version.World.NeedsDB && zpackage.ReadBool();

        // note: OMISSION, accepted, flagged here at its actual position
        // (contrast the class-header note further up, which is too far away
        // to catch on a patch-day diff):
        // `if (world != Version.World.DeepNorth) { m_createBackupBeforeSaving
        // = true; }` in source (World.cs:331-334 at 1.0.7) — a real per-patch
        // version-tracking site, consuming no bytes and setting a field this
        // mirror doesn't carry (filesystem-backup choreography, not format).
        // It has moved with the ceiling every patch (`!= 36` at 0.221.4,
        // `!= 37` at 0.221.10, and the named member for 41 now), which is
        // exactly why it is flagged here rather than only in the class header.
        // Same treatment PlayerProfile.cs gives its own equivalent omission.

        // note: OMISSION, accepted, NEW AT 1.0.7. Source also assigns
        // m_chunkedSave and then checks it against the
        // version at this point in the sequence: `if (flag != world >=
        // Version.World.ChunkedSave)` logs an error and abandons the load with
        // SaveDataError.BadVersion. It consumes no bytes, and `flag` comes
        // from SaveSystem.IsChunkedSave(path, source) — a fact about the
        // file's PATH SHAPE, not its payload — which this method cannot see:
        // it takes bytes, not a path. So the check is genuinely unavailable
        // here rather than merely skipped.
        // The consequence is the same shape as the CheckDbFile omission
        // documented in the class header: Norn will resolve identity from a
        // .fwl2 whose chunked-ness disagrees with its version, which the game
        // itself rejects outright. Accepted for the same reason — a world's
        // name and UID are readable either way, and this mirror never
        // inspects paths.

        if (ver >= Version.World.GlobalKeys)
        {
            int num = zpackage.ReadInt();
            for (int i = 0; i < num; i++)
            {
                m_startingGlobalKeys.Add(zpackage.ReadString());
            }
        }

        // note: NEW AT 1.0.7, and the only new block in the .fwl2 payload —
        // fields 1-8 above are byte-identical to the old .fwl. Source
        // reassigns m_playerHistory to a fresh List with the read count as
        // its capacity rather than clearing the existing one; that is
        // transcribed as written, and it also means this block needs no
        // equivalent of the m_startingGlobalKeys.Clear() above.
        if (ver >= Version.World.DeepNorth)
        {
            int num2 = zpackage.ReadInt();
            m_playerHistory = new List<ZNet.CrossNetworkUserInfo>(num2);
            for (int j = 0; j < num2; j++)
            {
                m_playerHistory.Add(ZNet.CrossNetworkUserInfo.Read(zpackage));
            }
        }

        return true;
    }

    // mirrors: World's own file-opening step inside LoadWorld (the .fwl
    // frame, prior to any ZPackage parsing) — not a named source method of
    // its own, same "frame reading kept separate from payload parsing" shape
    // as PlayerProfile.LoadPlayerDataFromDisk.
    // source:  Valheim 1.0.15
    // note:    FRAME DIVERGENCE FROM .fch. Confirmed directly: a .fwl is
    //          length-prefixed payload, then EOF — no
    //          trailing hash pair, unlike PlayerProfile's envelope. Nothing
    //          is captured for round-trip purposes (no TrailingBytes
    //          equivalent) since this is a read-only mirror with no write
    //          path, ever, to preserve bytes for.
    // note:    BEHAVIOURAL DIVERGENCE, same as Load above: exceptions
    //          propagate rather than being swallowed.
    // note:    NOT IN SOURCE — SHARING MODE. Opens with FileShare.ReadWrite |
    //          FileShare.Delete, not File.OpenRead's default FileShare.Read.
    //          One real source of .fwl reads is a Steam Cloud local mirror
    //          folder Steam's own client actively syncs —
    //          a plain FileShare.Read
    //          handle would tell Windows to deny that process a concurrent
    //          write/rename/delete on the same file for as long as Norn's
    //          handle is open, which could make Norn responsible for a failed
    //          or delayed sync. Verified empirically
    //          (Norn.Tests.WorldFileSharingTests): Windows denies any new open
    //          request for FileShare.None whenever another handle is already
    //          open, regardless of that handle's own share flags, so a writer
    //          demanding full exclusivity is still blocked by Norn's read no
    //          matter what this method requests. What this flag actually
    //          guarantees: whenever the other side's own write request is
    //          NOT fully exclusive (e.g. it still permits concurrent readers,
    //          plausible for a sync client), Norn's read no longer blocks it
    //          — the default File.OpenRead sharing mode would have, in every
    //          case. Real, meaningful, but not a substitute for timing
    //          avoidance (WorldIdentityShim's IsGameRunning gate) against the
    //          fully-exclusive case. Applies to every .fwl read through this
    //          method, not just the Steam-sourced ones — a strict
    //          improvement with no downside for worlds_local either.
    public static byte[] ReadPayloadFromDisk(string path)
    {
        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using BinaryReader binary = new BinaryReader(fileStream);

        int num = binary.ReadInt32();
        return binary.ReadBytes(num);
    }
}
