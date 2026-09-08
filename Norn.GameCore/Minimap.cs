using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: Minimap (decode and encode)
// source:  Valheim 1.0.7
// note:    WorldPlayerData.m_mapData is still opaque byte[] on the ordinary
//          PlayerProfile Read/Write round trip and is re-emitted verbatim for
//          every world a caller doesn't explicitly touch (R3) — that default
//          is unchanged. Decode/Encode exist so one narrow, explicit editing
//          path (CharacterEditor.ExploreAllMap, Norn.Adapter) can replace one
//          world's blob with a freshly-encoded one; re-encoding is no longer
//          permanently off the table, but the "must never appear on a
//          write path R1 covers" default in Norn.GameCore.Primitives.Utils
//          still holds everywhere else. The reasoning: gzip output is not
//          byte-reproducible across
//          runtimes, so Encode's output can never satisfy R1 against a
//          game-written blob — only R2 (this project's own
//          Decode(Encode(x)) round trip) is claimed for it, and only for the
//          one world an edit actually touches.
// note:    MAPVERSION MOVED OUT OF THIS TYPE AT 1.0.7. It was
//          `private static int MAPVERSION = 8` on Minimap through 0.221.10,
//          and SetMapData compared stream contents against bare int literals
//          rather than reading it. 1.0.7 deleted the field outright and
//          replaced every one of those literals with a member of the new
//          Version.Map enum — so the gates below now name what they mean, and
//          the constant lives with the other version constants in Version.cs.
//          The VALUE did not change (8, PinsAuthor); only its home and type
//          did. The alias kept below is for this project's own callers, not a
//          mirror of anything.
public static partial class Minimap
{
    // note: NOT IN SOURCE AS OF 1.0.7 — see the header note. Kept as an alias
    // onto Version.Map so existing call sites (the encoder below, and
    // Norn.Tests' MinimapEncodeTests) keep a plain int to write and compare,
    // and so a patch-day reader still finds a greppable name for "the map
    // blob format version". Do not treat this as a mirrored declaration; the
    // mirrored one is Version.Map.
    public const int MAPVERSION = (int)Version.c_MapVersion;

    // note: NOT IN SOURCE. A corruption-guard ceiling, not a format fact —
    // see the RISK note on Decode below for why this exists and how the
    // value was chosen. Not a stand-in for the real texture size; the
    // stored value is still what sizes the arrays, this only rejects sizes
    // past what any real save could plausibly contain.
    private const int MaxSaneTextureSize = 8192;

    // mirrors: Minimap.SetMapData(byte[])
    // source:  Valheim 1.0.7
    // note:    NAME/SIGNATURE DIVERGENCE. private void SetMapData(byte[]) in
    //          source, mutates a live Minimap component's fields in place and
    //          returns nothing. Here: public static MapData Decode(byte[]),
    //          returns a value — there is no live Minimap component for a
    //          standalone reader to mutate. Precedent for a value-returning
    //          decode in this codebase is PlayerProfile.LoadPlayerDataFromDisk
    //          (returns a SaveFileEnvelope), not LoadPlayerFromDisk (mutates
    //          `this`, returns a status bool) — this method's shape matches
    //          the former, not the latter.
    // note:    DIVERGENCE. Source compares the stored texture-size int
    //          against its own live Minimap component's m_textureSize and
    //          returns immediately (reading nothing further) on a mismatch —
    //          there is no live component to compare against here, and the
    //          stored value is the only truth available offline. This mirror
    //          instead sizes
    //          its own arrays from the stored value directly, with a minimal
    //          sanity check (below) substituting for the abort source's
    //          live-mismatch check would otherwise have provided.
    // note:    RISK, addressed in two parts. The stored size is
    //          attacker/corruption-influenced file content with no live value
    //          to validate it against. Part 1: an unchecked
    //          `textureSize * textureSize` can overflow `int` to negative
    //          (e.g. 46341) — guarded by `checked` arithmetic, which throws
    //          OverflowException rather than wrapping. Part 2, distinct
    //          hazard: a large-but-non-overflowing size (e.g. 40000, ~1.6e9
    //          cells) does not overflow but still drives four allocations of
    //          that size — guarded separately by MaxSaneTextureSize below,
    //          well above any known real value (2048, confirmed via
    //          AssetRipper) but far short of where
    //          arithmetic alone stops helping. Both throw
    //          (InvalidDataException / OverflowException) rather than
    //          silently misbehaving — a loud failure on corrupt input,
    //          matching this project's existing stance (see the RISK notes
    //          on Player.Load's Math.Clamp calls). CONVERGENT WITH SOURCE AS
    //          OF 1.0.7, where it used to be a divergence: source's
    //          texture-size mismatch (Minimap.cs:1895-1901) and
    //          ResetAndExplore's length mismatch (:1554-1558) now both
    //          ZLog.LogError and then THROW. Through 0.221.10 both logged a
    //          warning and `return`ed, abandoning the blob while leaving
    //          prior live state untouched, and this mirror threw anyway
    //          because a value-returning decoder has no prior state to fall
    //          back to. The reasoning is unchanged; the game has simply
    //          arrived at the same answer.
    // note:    OMISSION, addressed. Source's ResetAndExplore validates both
    //          mask arrays' lengths against the live fog texture's pixel
    //          count before touching any state, and abandons the blob on a
    //          mismatch (Minimap.cs, called from this method). Not a
    //          live-component-only concern: ZPackage.ReadByteArray(int)
    //          bottoms out in BinaryReader.ReadBytes, which returns a SHORT
    //          array at end-of-stream rather than throwing, so a truncated
    //          blob would otherwise silently produce a MapData whose
    //          Explored.Length is less than TextureSize * TextureSize with no
    //          error — dangerous for any consumer indexing by
    //          `y * TextureSize + x`. Guarded explicitly below (throws, which
    //          as of 1.0.7 is what source does too — see above).
    // note:    DECOMPOSITION. Source expresses this method's body across five
    //          named helpers it calls into: ResetAndExplore (1533 and 1549 —
    //          two overloads at 1.0.7, the bulk mask path + its length guard),
    //          Explore (1519, single-cell write, legacy path), Reset (clears
    //          both masks — the legacy path's zero-initialized arrays below
    //          are its only counterpart here), AddPin (2059, pin construction
    //          — the type-clamp and null-name notes on PinData describe what
    //          it does that this mirror doesn't), and ClearPins (called
    //          alongside the pin count read below — see the note there for
    //          why this mirror has no pin list to clear). All five are
    //          inlined directly into Decode rather than kept as separate
    //          methods, since none has a live-component reason to exist
    //          independently here — a future patch diff to any of those five
    //          source methods should land inside this one method body, not
    //          go looking for a same-named counterpart.
    // note:    OMISSION, trivial. Source ends SetMapData with
    //          `this.m_fogTexture.Apply();` — a Unity texture upload with no
    //          bearing on the decoded values and no live texture here. Not
    //          carried.
    // note:    Version gates, exactly:
    //          >=2 pin section, >=3 per-pin m_checked, >=4 trailing public-
    //          position flag, >=5 bulk mask read (else per-cell bool loop),
    //          >=6 per-pin m_ownerID, >=7 whole inner payload gzipped, >=8
    //          per-pin m_author. No gate exists at 1 or above 8. The >=3
    //          gate uses source's own `>= 3 && ReadBool()` short-circuit form
    //          (the one gate in this
    //          method source itself writes that way); >=6/>=8 mirror
    //          source's own ternaries, same reference. >=4 uses an `if`
    //          block, matching source's own `if` form there (source uses the
    //          && idiom only for the >=3 gate).
    public static MapData Decode(byte[] data)
    {
        ZPackage pkg = new ZPackage(data);
        Version.Map version = (Version.Map)pkg.ReadInt();

        if (version >= Version.Map.Compressed)
        {
            pkg = pkg.ReadCompressedPackage();
        }

        int textureSize = pkg.ReadInt();
        if (textureSize <= 0 || textureSize > MaxSaneTextureSize)
        {
            throw new InvalidDataException($"Map data blob has an unreasonable texture size ({textureSize}).");
        }

        int cellCount = checked(textureSize * textureSize);

        byte[] explored;
        byte[] exploredOthers;
        if (version >= Version.Map.NewExplore)
        {
            byte[] rawExplored = pkg.ReadByteArray(cellCount);
            byte[] rawExploredOthers = pkg.ReadByteArray(cellCount);
            if (rawExplored.Length != cellCount || rawExploredOthers.Length != cellCount)
            {
                throw new EndOfStreamException(
                    $"Map data blob truncated: expected {cellCount} bytes per exploration mask.");
            }

            // note: TYPE DIVERGENCE. Source holds these as BitArray as of
            // 1.0.7 (m_explored / m_exploredOthers, Minimap.cs:2914/2917),
            // normalizing the wire bytes through `> 0` into a BitArray and
            // then delegating to a second ResetAndExplore(BitArray, BitArray)
            // overload that did not exist before. Through 0.221.10 they were
            // bool[] normalized through a `!= 0` check. Held here as byte[] instead, unchanged
            // by any of that: both this branch and the legacy branch below
            // store the same 0/1 representation, the results are identical to
            // source's either way, and the wire byte itself is not guaranteed
            // to be exactly 0 or 1.
            explored = new byte[cellCount];
            exploredOthers = new byte[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                explored[i] = (byte)(rawExplored[i] != 0 ? 1 : 0);
                exploredOthers[i] = (byte)(rawExploredOthers[i] != 0 ? 1 : 0);
            }
        }
        else
        {
            explored = new byte[cellCount];
            exploredOthers = new byte[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                if (pkg.ReadBool())
                {
                    // note: Source decomposes into x/y against its own live
                    // m_textureSize, then recomposes via Explore's
                    // `y * m_textureSize + x` write — a genuine reshape only
                    // when the stored size and the live size differ. This
                    // mirror has a single texture-size value (the stored
                    // one, used both for the loop bound and here), so the
                    // decomposition is a no-op transform in this context —
                    // kept anyway to preserve source's shape for a future
                    // patch diff.
                    int x = i % textureSize;
                    int y = i / textureSize;
                    explored[y * textureSize + x] = 1;
                }
            }
        }

        // note: Source reads the pin count, then calls ClearPins() — both
        // inside the >= 2 gate; below that version no pin section exists in
        // the stream at all and existing pins survive untouched.
        // There is no live pin list
        // here to clear, so `pins` is simply declared empty and only
        // populated inside the gate — the observable result (nothing) is the
        // same, this comment is the only place that ordering detail survives.
        List<PinData> pins = new List<PinData>();
        if (version >= Version.Map.Pins)
        {
            int pinCount = pkg.ReadInt();
            for (int i = 0; i < pinCount; i++)
            {
                PinData pin = new PinData();
                pin.m_name = pkg.ReadString();
                pin.m_pos = pkg.ReadVector3();
                pin.m_type = (PinType)pkg.ReadInt();
                pin.m_checked = version >= Version.Map.PinsChecked && pkg.ReadBool();
                pin.m_ownerID = version >= Version.Map.PinsOwnerID ? pkg.ReadLong() : 0L;
                pin.m_author = version >= Version.Map.PinsAuthor ? pkg.ReadString() : "";
                pins.Add(pin);
            }
        }

        bool publicReferencePosition = false;
        if (version >= Version.Map.VisibleOnMap)
        {
            publicReferencePosition = pkg.ReadBool();
        }

        return new MapData
        {
            Version = (int)version,
            TextureSize = textureSize,
            Explored = explored,
            ExploredOthers = exploredOthers,
            Pins = pins,
            PublicReferencePosition = publicReferencePosition
        };
    }

    // mirrors: Minimap.GetMapData()
    // source:  Valheim 1.0.7
    // note:    NAME/SIGNATURE DIVERGENCE, same reasoning as Decode above:
    //          private byte[] GetMapData() in source reads from a live
    //          Minimap component's own fields; there is no live component to
    //          read from here, so this mirror takes the value in and returns
    //          the bytes instead of mutating anything in place.
    // note:    DECOMPOSITION DIVERGENCE. Source's write-side pin filter is
    //          `m_save == true`, counted in a separate pass before the write
    //          loop. PinData here carries no m_save field (see PinData's own
    //          note — it's live-runtime-only, never carried); verified
    //          against every AddPin call site in source that this mirror
    //          needed to check that no offline-constructible pin ever has
    //          m_save == false, so
    //          the count and the write loop below both treat every entry
    //          unconditionally: `data.Pins.Count` for the count,
    //          `data.Pins` itself for the write loop.
    // note:    DIVERGENCE. Source's final bool write reads
    //          ZNet.instance.IsReferencePositionPublic() live from the
    //          network singleton. There is no singleton here; this mirror
    //          writes data.PublicReferencePosition instead — the same field
    //          Decode populates from the equivalent read.
    // note:    DIVERGENCE. Source writes `pinData.m_author.ToString()` —
    //          m_author is a PlatformUserID there. This mirror's
    //          PinData.m_author is already a plain string (PinData's own
    //          note: PlatformUserID isn't present in any decompiled assembly,
    //          so Decode holds the wire string directly, never wrapped) —
    //          there is no ToString() to call, only the string itself to
    //          write back. Unverified for the empty/None case specifically:
    //          PlatformUserID.None.ToString()'s actual value can't be checked
    //          without that type's source. Bounded impact either way —
    //          SetMapData maps an empty-or-null author string back to
    //          PlatformUserID.None on read (Minimap.cs), so the blob stays
    //          loadable regardless of which byte sequence None.ToString()
    //          actually produces.
    // note:    Quirk carried from source: the second mask-write loop below
    //          iterates data.Explored.Length, not data.ExploredOthers.Length —
    //          matches GetMapData's own literal bound (m_explored.Length used
    //          for both loops). Safe in source because m_explored/
    //          m_exploredOthers are private fields always allocated together
    //          at Start() — not structurally guaranteed here, since MapData
    //          exposes both as public mutable fields with no invariant
    //          enforcing equal length. Correct today because the sole caller
    //          (CharacterEditor.ExploreAllMap) only Array.Fills Explored in
    //          place, never resizes either array; a caller that resized one
    //          without the other would hit IndexOutOfRangeException here
    //          rather than anything source would do. Kept anyway for
    //          diffability against a future patch.
    // note:    MAPVERSION is written unconditionally, regardless of
    //          data.Version — matches source exactly (GetMapData never reads
    //          the incoming version, only ever emits the current constant).
    //          Consequence: encoding a decoded blob whose Version was below 8
    //          upgrades it to 8. For the three gated pin fields (m_checked,
    //          m_ownerID, m_author) this exactly matches what the live
    //          game's own next save would emit, since AddPin receives the
    //          same defaults Decode already uses for those gates. It does
    //          NOT hold the same way for PublicReferencePosition: source
    //          writes the live flag every time, which a version < 4 blob
    //          never fed (SetPublicReferencePosition sits inside the >= 4
    //          gate) and could legitimately be true in a live session at
    //          save time. Decode's `false` for that case is an invented
    //          default (MapData's own note), and this method then persists
    //          it as though it were a stored fact — a real, if narrow,
    //          divergence for any map blob below version 4 (unreachable by
    //          the current corpus). Irreversible either way, once encoded.
    // note:    RISK. WriteCompressed's gzip output is not byte-reproducible
    //          against the game's own Mono runtime (Norn.GameCore.Primitives.Utils),
    //          so this method's output can satisfy R2 (Decode(Encode(x))
    //          round trip) but never R1 against a game-written blob.
    //          CharacterEditor.ExploreAllMap is
    //          the only caller, and only ever invokes this for a world whose
    //          map the user explicitly chose to modify — every other world's
    //          m_mapData stays the untouched opaque byte[] R3 already
    //          guarantees.
    public static byte[] Encode(MapData data)
    {
        ZPackage outer = new ZPackage();
        outer.Write(MAPVERSION);

        ZPackage inner = new ZPackage();
        inner.Write(data.TextureSize);

        for (int i = 0; i < data.Explored.Length; i++)
        {
            inner.Write(data.Explored[i] != 0);
        }

        for (int i = 0; i < data.Explored.Length; i++)
        {
            inner.Write(data.ExploredOthers[i] != 0);
        }

        inner.Write(data.Pins.Count);
        foreach (PinData pin in data.Pins)
        {
            inner.Write(pin.m_name);
            inner.Write(pin.m_pos);
            inner.Write((int)pin.m_type);
            inner.Write(pin.m_checked);
            inner.Write(pin.m_ownerID);
            inner.Write(pin.m_author);
        }

        inner.Write(data.PublicReferencePosition);

        outer.WriteCompressed(inner);
        return outer.GetArray();
    }
}
