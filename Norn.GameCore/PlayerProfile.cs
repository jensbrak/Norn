using Norn.GameCore.Primitives;

namespace Norn.GameCore;

/// <summary>
/// One character save: the profile envelope, and the inner player-data blob
/// (<see cref="m_playerData"/>) it carries.
/// </summary>
public partial class PlayerProfile
{
    // mirrors: PlayerProfile..ctor(string)
    // source:  Valheim 1.0.15
    // note:    OMISSION, partially accepted. Source's constructor also sets
    //          m_playerID = Utils.GenerateUID() after m_playerName. Both fields
    //          are read ungated at every supported version, so a loaded profile
    //          is unaffected either way; GenerateUID() (a process-global
    //          counter/random source) is not mirrored since it has no bearing
    //          on reading an existing file. "Stranger" is kept for diffability;
    //          the ID stays at its field default (0).
    // note:    The m_playerStats fill loop is new at 1.0.7 and is NOT optional
    //          the way the omissions above are: the field initializer allocates
    //          the array but leaves all ten elements null, so without this loop
    //          every read into m_playerStats[i] would throw. Source's loop bound
    //          is a hardcoded 10, not DifficultyRequirement.Count — kept as
    //          written.
    public PlayerProfile(string filename)
    {
        m_filename = filename;
        m_playerName = "Stranger";
        for (int i = 0; i < 10; i++)
        {
            m_playerStats[i] = new PlayerStats();
        }
    }

    // mirrors: PlayerProfile field declarations, in source's own declaration
    // order (which differs from LoadPlayerFromDisk's read order below).
    // source:  Valheim 1.0.15
    // note:    NOT AN EXHAUSTIVE FIELD LIST. Only fields reachable from
    //          LoadPlayerFromDisk/SavePlayerToDisk are carried. Source declares
    //          several more with no bearing on the byte format at all, none
    //          mirrored here: m_fileSource, m_createBackupBeforeSaving, and
    //          m_lastSaveLoad are read or set during load but contribute no
    //          bytes (m_fileSource selects a path-composition strategy this
    //          standalone reader doesn't use — see the note on
    //          LoadPlayerDataFromDisk below; m_createBackupBeforeSaving governs
    //          the separate *timestamped* backup taken only when the version read at
    //          load was not the current one (46 at 1.0.7, expressed as a named enum
    //          member — it was a literal 43 at 0.221.10) — not the unconditional `.old`
    //          rotation, which is a different mechanism entirely (see the note on
    //          SavePlayerToDisk's envelope overload); m_lastSaveLoad is a
    //          runtime timestamp never written to disk).
    //          SavingStarted/SavingFinished (static Actions, invoked by
    //          SavePlayerToDisk as choreography — see the note there)
    //          contribute no bytes either way. m_originalSpawnPoint (static,
    //          UI/dev-profile default only) and m_statTypeDates (static, UI
    //          label metadata) are untouched by load/save entirely. Same
    //          "filesystem choreography, not format" reasoning already applied
    //          to SavePlayerToDisk's rotation dance.
    // note:    VISIBILITY. Fields private in source (m_worldData, m_playerName,
    //          m_playerID, m_startSeed, m_playerData) are made public here so
    //          Adapter can reach them directly — the game's own
    //          private-with-property-accessor style has no bearing on the wire
    //          format. Fields already public in source stay public.
    // note:    TYPE DIVERGENCE. m_worldData is declared as Dictionary<TKey,
    //          TValue> in source. Held here as List<KeyValuePair<TKey, TValue>>
    //          instead — .NET does not contract Dictionary's enumeration order,
    //          so a writer that re-enumerates one cannot guarantee byte
    //          identity. OrderedCollections.Add/Set reproduce Dictionary.Add's
    //          throw-on-duplicate and the indexer's overwrite-in-place
    //          semantics on top of the ordered list. The six string/float
    //          dictionaries this note also used to cover are no longer declared
    //          here at all — 1.0.7 moved them into PlayerStats, where the same
    //          divergence applies for the same reason and is documented on that
    //          class.
    // note:    m_playerStats became PlayerStats[10] at 1.0.7, indexed by
    //          DifficultyRequirement (RawStats=0, Any=1, Hammer=2, Casual=3 …
    //          Hardcore=9). Source declares the array here and fills it in the
    //          constructor, not in the field initializer; both are mirrored.
    //          `readonly` matches source.
    public readonly PlayerStats[] m_playerStats = new PlayerStats[10];

    public bool m_firstSpawn = true;

    // note:    Public and readonly in source, set once in the constructor for
    //          the object's whole lifetime — a real PlayerProfile is
    //          permanently tied to one file. Narrowed to internal and made
    //          non-readonly here, so the round-trip harness can load from a
    //          corpus path and redirect the same instance's write to a
    //          temporary path without duplicating every field onto a second
    //          instance. Declared here, at source's position between the
    //          (not-carried) m_fileSource and m_playerName.
    internal string m_filename = "";

    public string m_playerName = "";

    public long m_playerID;

    public string m_startSeed = "";

    public readonly List<KeyValuePair<long, WorldPlayerData>> m_worldData = new List<KeyValuePair<long, WorldPlayerData>>();

    public bool m_usedCheats;

    public DateTime m_dateCreated = DateTime.Now;

    /// <summary>
    /// The inner player-data blob. Decoded by <see cref="Player"/>.
    /// </summary>
    public byte[] m_playerData;

    // note:    NOT IN SOURCE. m_dateCreated (ver >= 38) is derived from a Unix-
    //          seconds Int64 via DateTimeOffset.FromUnixTimeSeconds(...).Date —
    //          truncating to a date discards the time-of-day irrecoverably. The
    //          game's own write side then reconstructs the Int64 via
    //          `new DateTimeOffset(m_dateCreated).ToUnixTimeSeconds()`, and that
    //          constructor treats a Kind=Unspecified DateTime as LOCAL time. On
    //          any host east or west of UTC this recomputes a different Unix
    //          timestamp than the one on disk — a genuine bug in the game, not a
    //          decompilation artefact, and a concrete R1/R2 hazard independent of
    //          host timezone. Fixed here by treating the raw Int64 as the
    //          authoritative value: captured verbatim on load, re-emitted
    //          verbatim on save, never recomputed from m_dateCreated. For the
    //          ver < 38 legacy branch (no Int64 on disk at all), a value is
    //          synthesized from the literal 2021-02-02 default using an explicit
    //          UTC offset (not the game's local-time-assuming constructor), which
    //          round-trips deterministically regardless of host timezone.
    public long DateCreatedUnixSeconds;

    // note:    NOT IN SOURCE. The game writes nothing after the hash and never
    //          reads past it (see SaveFileEnvelope.TrailingBytes); captured here
    //          purely so a hypothetical non-empty tail still round-trips (R3).
    private byte[] TrailingBytes = Array.Empty<byte>();

    // note:    NOT IN SOURCE. The game reads `ver` into a local variable and
    //          never retains it — no field. Captured here because it decides
    //          which R1/R2 doctrine applies to a given file (R1 only when the
    //          on-disk version equals the writer version), and because the UI
    //          needs a save version to display.
    public int ProfileVersion { get; private set; }

    // mirrors: PlayerProfile.LoadPlayerDataFromDisk()
    // source:  Valheim 1.0.15
    // note:    m_filename holds a full path here, where the game holds a bare
    //          character name and composes the path through
    //          SaveSystem.GetCharacterPath(m_fileSource, m_filename) — called
    //          directly at 1.0.7; at 0.221.10 the chain went through GetPath ->
    //          GetCharacterFolderPath(m_fileSource). Either way it bottoms out
    //          in Utils.GetSaveDataPath and Unity's persistent-data path,
    //          neither of which a standalone reader has; and save-directory
    //          resolution is the UI's job as a pure function over (platform,
    //          home directory). The four reads below are unchanged.
    // note:    The game opens the file through FileReader, which for a local
    //          source is File.OpenRead, a BufferedStream over it (262144 bytes,
    //          added at 1.0.7 — it was File.OpenRead straight into a
    //          BinaryReader at 0.221.10), and a BinaryReader over that. No
    //          header, no seek, no transformation, and buffering changes
    //          nothing about the bytes delivered. FileReader is present in the
    //          1.0.7 and 0.221.10 trees but absent from the older 0.221.4 tree
    //          this layer was originally built against, so it's inlined here
    //          rather than given its own mirrored type for a short body.
    //          Reading starts at byte 0 either way.
    // note:    BEHAVIOURAL DIVERGENCE. The game wraps this in try/catch, logs,
    //          and returns null on any failure; its caller then wraps the whole
    //          parse in a second catch that still returns true, leaving a
    //          half-populated profile the game treats as loaded. Exceptions
    //          propagate here instead. A viewer that silently shows a truncated
    //          character is worse than one that says the file is broken, and a
    //          swallowed exception would make R1 failures unreadable.
    // note:    VISIBILITY DIVERGENCE. private in source, returning a ZPackage;
    //          public here, returning our own SaveFileEnvelope so the hash and
    //          any trailing bytes the game discards survive for R3.
    // note:    OMISSION, accepted. Three source methods on this path have no
    //          counterpart: SavePlayerData(Player)/LoadPlayerData(Player) (the
    //          profile↔Player bridge — LoadPlayerData also calls
    //          player.SetPlayerID(...) and branches on m_playerData being null
    //          to call GiveDefaultItems() instead of Load) and the
    //          parameterless Save() (source: `m_filename != null &&
    //          SavePlayerToDisk()`, mirroring Load()'s own shape exactly).
    //          The round-trip harness wires PlayerProfile.m_playerData
    //          directly into a fresh Player's Load/Save instead of going
    //          through this bridge — sufficient for round-trip testing, but a
    //          real gap against cheap patch-day updates: a future patch touching
    //          LoadPlayerData's branch has nowhere to land here. Revisit in
    //          Adapter, which needs this bridge to construct a
    //          Player from a loaded profile anyway.
    /// <summary>Reads the file frame. The payload is not interpreted here.</summary>
    public SaveFileEnvelope LoadPlayerDataFromDisk()
    {
        using FileStream fileStream = File.OpenRead(m_filename);
        using BinaryReader binary = new BinaryReader(fileStream);

        int num = binary.ReadInt32();
        byte[] array = binary.ReadBytes(num);
        int num2 = binary.ReadInt32();
        // The game reads these and discards them. Captured instead — see
        // SaveFileEnvelope for why.
        byte[] hash = binary.ReadBytes(num2);
        byte[] trailingBytes = binary.ReadBytes((int)(fileStream.Length - fileStream.Position));

        return new SaveFileEnvelope(array, num2, hash, trailingBytes);
    }

    // mirrors: PlayerProfile.Load()
    // source:  Valheim 1.0.15
    /// <summary>Loads the profile envelope. The inner player-data blob stays opaque.</summary>
    public bool Load()
    {
        return m_filename != null && LoadPlayerFromDisk();
    }

    // mirrors: PlayerProfile.LoadPlayerFromDisk()
    // source:  Valheim 1.0.15
    // note:    BEHAVIOURAL DIVERGENCE, same as LoadPlayerDataFromDisk above: the
    //          game wraps this whole body in a try/catch that logs and swallows,
    //          still returning true. Exceptions propagate here instead.
    // note:    OMISSION, trivial. Source opens with `Stopwatch.StartNew();`
    //          whose result is discarded — dead timing scaffolding, no bytes,
    //          no observable effect. Not carried.
    // note:    OMITTED VERSION COMPARISON. Immediately after the compatibility
    //          check, source has `if (player != Version.Player.DeepNorth)
    //          { m_createBackupBeforeSaving = true; }` — a real version
    //          comparison that has tracked the ceiling at every patch so far
    //          (`!= 42` at 0.221.4, `!= 43` at 0.221.10, now the named member
    //          for 46), but the field it sets contributes no bytes (see the
    //          field-declaration note above), so it has no counterpart here.
    //          Flagged at its actual position for patch-day visibility, since
    //          the field note alone is easy to miss when diffing this body.
    // note:    THE STATISTICS GATE IS NOT MONOTONIC, and this is the single
    //          most damaging thing to get wrong in this method. The new
    //          two-dimensional block is gated
    //          `ver >= DeepNorth(46) || ver == AbandonedDN(44)` — version
    //          **45 (Chunked) is excluded** and falls through to the legacy
    //          flat array. The chronology is real, not a decompiler artefact:
    //          44 introduced the layout, 45 reverted to the old one, 46
    //          reinstated it. The late metadata block below inverts the same
    //          condition (`ver < DeepNorth && ver != AbandonedDN`). Writing
    //          either as a plain `>=` threshold silently corrupts every save
    //          written by one specific game build. No corpus file exists at 44
    //          or 45, so nothing here is covered by a test — it is carried on
    //          source-reading alone and must be re-read, not assumed, on the
    //          next patch.
    // note:    Nine inline metadata-collection loops per stat block (knownWorlds,
    //          knownWorldKeys, knownCommands, the five enemyStats dictionaries,
    //          itemPickupStats, itemCraftStats, pickableStats, foodEatenStats,
    //          piecesPlacedStats) are written out separately rather than behind
    //          a shared helper, since source itself is one method with no
    //          sub-methods for any of these blocks; an invented abstraction over
    //          the read sequence would make the next patch diff harder to read,
    //          not easier. Was six loops on the profile itself at 0.221.10.
    // note:    LOOP-VARIABLE NAMES. Source's decompiled body reuses one
    //          temporary (`num3`) for every collection count and names its loop
    //          variables i/j/k/l/m/n/num5..num10. Those are decompiler register
    //          artefacts rather than real identifiers, so this file keeps the
    //          descriptive-name convention it already used at 0.221.10. The
    //          read *order* is transcribed exactly, which is the part that
    //          matters.
    private bool LoadPlayerFromDisk()
    {
        SaveFileEnvelope envelope = LoadPlayerDataFromDisk();
        TrailingBytes = envelope.TrailingBytes;
        ZPackage zpackage = new ZPackage(envelope.PlayerData);

        Version.Player ver = (Version.Player)zpackage.ReadInt();
        ProfileVersion = (int)ver;
        if (!Version.IsPlayerVersionCompatible(ver))
        {
            return false;
        }

        if (ver >= Version.Player.DeepNorth || ver == Version.Player.AbandonedDN)
        {
            int statCount = zpackage.ReadInt();
            int blockCount = zpackage.ReadInt();
            for (int i = 0; i < blockCount; i++)
            {
                for (int j = 0; j < statCount; j++)
                {
                    m_playerStats[i][(PlayerStatType)j] = zpackage.ReadSingle();
                }

                int knownWorldsCount = zpackage.ReadInt();
                for (int k = 0; k < knownWorldsCount; k++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_knownWorlds, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int knownWorldKeysCount = zpackage.ReadInt();
                for (int l = 0; l < knownWorldKeysCount; l++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_knownWorldKeys, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int knownCommandsCount = zpackage.ReadInt();
                for (int m = 0; m < knownCommandsCount; m++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_knownCommands, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int enemyStatsDictCount = zpackage.ReadInt();
                for (int n = 0; n < enemyStatsDictCount; n++)
                {
                    int enemyStatsCount = zpackage.ReadInt();
                    for (int i2 = 0; i2 < enemyStatsCount; i2++)
                    {
                        OrderedCollections.Set(m_playerStats[i].m_enemyStats[n], zpackage.ReadString(), zpackage.ReadSingle());
                    }
                }

                int itemPickupStatsCount = zpackage.ReadInt();
                for (int i3 = 0; i3 < itemPickupStatsCount; i3++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_itemPickupStats, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int itemCraftStatsCount = zpackage.ReadInt();
                for (int i4 = 0; i4 < itemCraftStatsCount; i4++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_itemCraftStats, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int pickableStatsCount = zpackage.ReadInt();
                for (int i5 = 0; i5 < pickableStatsCount; i5++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_pickableStats, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int foodEatenStatsCount = zpackage.ReadInt();
                for (int i6 = 0; i6 < foodEatenStatsCount; i6++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_foodEatenStats, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int piecesPlacedStatsCount = zpackage.ReadInt();
                for (int i7 = 0; i7 < piecesPlacedStatsCount; i7++)
                {
                    OrderedCollections.Set(m_playerStats[i].m_piecesPlacedStats, zpackage.ReadString(), zpackage.ReadSingle());
                }
            }
        }
        else if (ver >= Version.Player.Stats2)
        {
            int statCount = zpackage.ReadInt();
            for (int i = 0; i < statCount; i++)
            {
                m_playerStats[0][(PlayerStatType)i] = zpackage.ReadSingle();
            }
        }
        else if (ver >= Version.Player.Stats)
        {
            m_playerStats[0][PlayerStatType.EnemyKills] = zpackage.ReadInt();
            m_playerStats[0][PlayerStatType.Deaths] = zpackage.ReadInt();
            m_playerStats[0][PlayerStatType.CraftsOrUpgrades] = zpackage.ReadInt();
            m_playerStats[0][PlayerStatType.Builds] = zpackage.ReadInt();
        }

        if (ver >= Version.Player.FirstSpawn)
        {
            m_firstSpawn = zpackage.ReadBool();
        }

        m_worldData.Clear();
        int worldCount = zpackage.ReadInt();
        for (int i = 0; i < worldCount; i++)
        {
            long worldUID = zpackage.ReadLong();
            WorldPlayerData worldPlayerData = new WorldPlayerData();
            worldPlayerData.m_haveCustomSpawnPoint = zpackage.ReadBool();
            worldPlayerData.m_spawnPoint = zpackage.ReadVector3();
            worldPlayerData.m_haveLogoutPoint = zpackage.ReadBool();
            worldPlayerData.m_logoutPoint = zpackage.ReadVector3();
            if (ver >= Version.Player.DeathPoint)
            {
                worldPlayerData.m_haveDeathPoint = zpackage.ReadBool();
                worldPlayerData.m_deathPoint = zpackage.ReadVector3();
            }

            worldPlayerData.m_homePoint = zpackage.ReadVector3();
            if (ver >= Version.Player.MapData && zpackage.ReadBool())
            {
                worldPlayerData.m_mapData = zpackage.ReadByteArray();
            }

            OrderedCollections.Add(m_worldData, worldUID, worldPlayerData);
        }

        SetName(zpackage.ReadString());
        m_playerID = zpackage.ReadLong();
        m_startSeed = zpackage.ReadString();

        if (ver >= Version.Player.Stats2)
        {
            m_usedCheats = zpackage.ReadBool();

            long dateCreatedUnixSeconds = zpackage.ReadLong();
            DateCreatedUnixSeconds = dateCreatedUnixSeconds;
            m_dateCreated = DateTimeOffset.FromUnixTimeSeconds(dateCreatedUnixSeconds).Date;

            if (ver < Version.Player.DeepNorth && ver != Version.Player.AbandonedDN)
            {
                int knownWorldsCount = zpackage.ReadInt();
                for (int i = 0; i < knownWorldsCount; i++)
                {
                    OrderedCollections.Set(m_playerStats[0].m_knownWorlds, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int knownWorldKeysCount = zpackage.ReadInt();
                for (int i = 0; i < knownWorldKeysCount; i++)
                {
                    OrderedCollections.Set(m_playerStats[0].m_knownWorldKeys, zpackage.ReadString(), zpackage.ReadSingle());
                }

                int knownCommandsCount = zpackage.ReadInt();
                for (int i = 0; i < knownCommandsCount; i++)
                {
                    OrderedCollections.Set(m_playerStats[0].m_knownCommands, zpackage.ReadString(), zpackage.ReadSingle());
                }

                if (ver >= Version.Player.CallToArms)
                {
                    int enemyStatsCount = zpackage.ReadInt();
                    for (int i = 0; i < enemyStatsCount; i++)
                    {
                        OrderedCollections.Set(m_playerStats[0].m_enemyStats[0], zpackage.ReadString(), zpackage.ReadSingle());
                    }

                    int itemPickupStatsCount = zpackage.ReadInt();
                    for (int i = 0; i < itemPickupStatsCount; i++)
                    {
                        OrderedCollections.Set(m_playerStats[0].m_itemPickupStats, zpackage.ReadString(), zpackage.ReadSingle());
                    }

                    int itemCraftStatsCount = zpackage.ReadInt();
                    for (int i = 0; i < itemCraftStatsCount; i++)
                    {
                        OrderedCollections.Set(m_playerStats[0].m_itemCraftStats, zpackage.ReadString(), zpackage.ReadSingle());
                    }
                }
            }
        }
        else
        {
            m_dateCreated = new DateTime(2021, 2, 2);
            DateCreatedUnixSeconds = new DateTimeOffset(m_dateCreated, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        if (zpackage.ReadBool())
        {
            m_playerData = zpackage.ReadByteArray();
        }
        else
        {
            m_playerData = null;
        }

        if (ver < Version.Player.FirstSpawn)
        {
            GetFirstSpawnFromPlayerData();
        }

        return true;

        // mirrors: <LoadPlayerFromDisk>g__GetFirstSpawnFromPlayerData|10_0
        // source:  Valheim 1.0.15
        // note:    Decompiler-surfaced local function; source name restated per
        //          the source reading. Peeks into the m_playerData blob without
        //          disturbing the real parse of it — a fresh ZPackage is
        //          constructed over the same bytes.
        void GetFirstSpawnFromPlayerData()
        {
            if (m_playerData == null)
            {
                return;
            }

            ZPackage innerPackage = new ZPackage(m_playerData);
            Version.PlayerData v = (Version.PlayerData)innerPackage.ReadInt();
            if (v < Version.PlayerData.FirstSpawn || v >= Version.PlayerData.MovedFirstSpawn)
            {
                return;
            }

            // note: The first of these two reads was gated `v >= 7` through
            // 0.221.10. 1.0.7 deleted that gate — the outer `v < 8` guard
            // above already made it always true, and the decompiler now shows
            // two unconditional reads. Byte-identical either way; carried as
            // source now writes it, since this file claims to mirror 1.0.7.
            innerPackage.ReadSingle();
            innerPackage.ReadSingle();

            if (v >= Version.PlayerData.MaxStamina)
            {
                innerPackage.ReadSingle();
            }

            m_firstSpawn = innerPackage.ReadBool();
        }
    }

    // mirrors: PlayerProfile.SetName(string)
    // source:  Valheim 1.0.15
    public void SetName(string name)
    {
        m_playerName = name;
    }

    // mirrors: PlayerProfile.SavePlayerToDisk() [profile-level fields; the
    // envelope frame below carries the final hash + length prefixes]
    // source:  Valheim 1.0.15
    // note:    Writer-only side effects the game performs are NOT replicated,
    //          and they grew at 1.0.7: the game now increments the current
    //          world's play seconds in TWO stat slots (slot 0 and the current
    //          achievement-difficulty slot) and rebuilds m_knownWorldKeys for
    //          both by looping GlobalKeys 0..52. A read-only mirror re-emits
    //          values exactly as read.
    // note:    THE `53` IN SOURCE'S WRITE PATH IS THE GLOBALKEYS LOOP BOUND,
    //          NOT A STAT COUNT — it sits a few lines from the real stat loop
    //          and was 43 at 0.221.10, which makes `43 -> 53` look exactly like
    //          a stat-count bump in a diff. It is not. The stat count is the
    //          literal written by `Write(205)` (was 105), and neither number
    //          has ever been the GlobalKeys bound. A patch-day reconnaissance
    //          pass mistook one for the other on this very upgrade; the note is
    //          here so the next reader does not repeat it.
    // note:    The nine per-slot collection-writing loops sit inside the
    //          10-iteration stat-block loop, mirroring the load side exactly.
    //          At 0.221.10 there were six such loops and they lived at the END
    //          of the payload, after the date field; 1.0.7 moved them into the
    //          statistics block, so the tail of this method is now shorter, not
    //          longer. A diff that only looks at the tail will read as a
    //          deletion.
    // note:    Source's loop bounds here are hardcoded literals (10, 205, 5),
    //          not the counts it just wrote and not DifficultyRequirement.Count
    //          / PlayerStatType.Count / KillModifiers.CountNone. The read side
    //          is data-driven off the written counts; the write side is not.
    //          Both are transcribed as written — do not "unify" them.
    // note:    VISIBILITY/SIGNATURE DIVERGENCE. private bool in source,
    //          returning true unconditionally; its only caller, the unmirrored
    //          Save(), propagates that value, but Save()'s own two call sites
    //          (Game.cs, FejdStartup.cs) use it as a plain expression
    //          statement and discard it there — so the value is genuinely
    //          unused game-wide, just not by the caller one frame up. internal
    //          void here: the write path is widened per field, deliberately,
    //          never wholesale — see the note on the envelope overload below.
    internal void SavePlayerToDisk()
    {
        ZPackage payload = new ZPackage();

        // note: bare literal, not Version.m_playerVersion — same reasoning
        // Version.cs gives for its own IsPlayerVersionCompatible: a decompiled
        // const int is inlined regardless of whether source used the named
        // field or a literal at this call site, so the bare-literal reading
        // is the consistent one across every version-literal site in this
        // project, not evidence about source's own form at this one.
        payload.Write(46);

        payload.Write(205);
        payload.Write(10);
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 205; j++)
            {
                payload.Write(m_playerStats[i].m_stats[(PlayerStatType)j]);
            }

            payload.Write(m_playerStats[i].m_knownWorlds.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_knownWorlds)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_knownWorldKeys.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_knownWorldKeys)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_knownCommands.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_knownCommands)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(5);
            for (int k = 0; k < 5; k++)
            {
                payload.Write(m_playerStats[i].m_enemyStats[k].Count);
                foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_enemyStats[k])
                {
                    payload.Write(pair.Key);
                    payload.Write(pair.Value);
                }
            }

            payload.Write(m_playerStats[i].m_itemPickupStats.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_itemPickupStats)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_itemCraftStats.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_itemCraftStats)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_pickableStats.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_pickableStats)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_foodEatenStats.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_foodEatenStats)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }

            payload.Write(m_playerStats[i].m_piecesPlacedStats.Count);
            foreach (KeyValuePair<string, float> pair in m_playerStats[i].m_piecesPlacedStats)
            {
                payload.Write(pair.Key);
                payload.Write(pair.Value);
            }
        }

        payload.Write(m_firstSpawn);

        payload.Write(m_worldData.Count);
        foreach (KeyValuePair<long, WorldPlayerData> pair in m_worldData)
        {
            payload.Write(pair.Key);
            payload.Write(pair.Value.m_haveCustomSpawnPoint);
            payload.Write(pair.Value.m_spawnPoint);
            payload.Write(pair.Value.m_haveLogoutPoint);
            payload.Write(pair.Value.m_logoutPoint);
            payload.Write(pair.Value.m_haveDeathPoint);
            payload.Write(pair.Value.m_deathPoint);
            payload.Write(pair.Value.m_homePoint);
            payload.Write(pair.Value.m_mapData != null);
            if (pair.Value.m_mapData != null)
            {
                payload.Write(pair.Value.m_mapData);
            }
        }

        payload.Write(m_playerName);
        payload.Write(m_playerID);
        payload.Write(m_startSeed);

        payload.Write(m_usedCheats);
        payload.Write(DateCreatedUnixSeconds);

        if (m_playerData != null)
        {
            payload.Write(true);
            payload.Write(m_playerData);
        }
        else
        {
            payload.Write(false);
        }

        SavePlayerToDisk(new SaveFileEnvelope(payload.GetArray(), 0, Array.Empty<byte>(), TrailingBytes));
    }

    // mirrors: PlayerProfile.SavePlayerToDisk(), envelope only
    // source:  Valheim 1.0.15
    // note:    Only the final four writes are mirrored — GenerateHash, GetArray,
    //          then length/payload/length/hash. Everything else in the game's
    //          method is either payload serialisation or
    //          filesystem choreography that contributes no bytes: the
    //          SavingStarted/SavingFinished callbacks,
    //          SaveSystem.PreSaveCloudChecksAndOperations, cache
    //          invalidation, ZNet.ConsiderAutoBackup, and the cloud fallback,
    //          plus TWO distinct backup mechanisms source has, not one:
    //            1. FileHelpers.ReplaceOldFile's .new/.old rotation — stages
    //               the write to a temp <name>.fch.new, then rotates the
    //               previous save to <name>.fch.old. UNCONDITIONAL on every
    //               local save, regardless of version.
    //            2. A separate timestamped <name>_backup_yyyyMMdd-HHmmss
    //               file. REWRITTEN AT 1.0.7, and the rewrite changed the
    //               conclusion: the whole mechanism is now UNREACHABLE for
    //               character saves. SavePlayerToDisk no longer calls
    //               SaveSystem.CheckMove/CreateBackup itself; it passes
    //               m_createBackupBeforeSaving into
    //               SaveSystem.PreSaveCloudChecksAndOperations, and the
    //               timestamped-backup path inside that is gated
    //               `flag2 && dataType == SaveDataType.World`. This call site
    //               passes SaveDataType.Character, so the flag is consumed
    //               and cleared and nothing is written. (For the world path
    //               that does reach it, the non-chunked branch is
    //               MoveToBackup — a rename — with CreateBackup, a copy, as
    //               the else.) Through 0.221.10 this was a live three-condition
    //               mechanism gated on m_createBackupBeforeSaving (set when
    //               the version READ at load was not the current one — see the
    //               OMITTED VERSION COMPARISON note on LoadPlayerFromDisk),
    //               !SaveSystem.CheckMove(...), and
    //               SaveSystem.TryGetSaveByName(...) succeeding on a
    //               non-deleted result. Recorded in full because "this is
    //               unreachable today" is exactly the kind of claim a future
    //               patch can quietly invalidate.
    //          That "contributes no bytes" reasoning holds for what THIS
    //          method does — it stays a pure format writer — but as of
    //          today this write path is reachable from real user saves, not
    //          only the round-trip harness, so mechanism 1's recoverability
    //          had to go somewhere: CharacterEditor.Save() in Norn.Adapter
    //          copies the file to ".old" immediately before calling this,
    //          unconditionally — reproducing only mechanism 1's *rotation*
    //          half (the previous file survives as .old), not its *staging*
    //          half (source writes to a temp file first and swaps; this
    //          mirror truncates the live file directly via File.Create below,
    //          so a crash mid-write can still leave the live file corrupt —
    //          recoverable from .old, but not automatically). Deliberately
    //          outside GameCore since it's I/O safety policy, not part of the
    //          save format. Mechanism 2 (the timestamped backup) is NOT
    //          reproduced anywhere, and as of 1.0.7 that is no longer even a
    //          gap: the game does not produce one for a character save either
    //          (see above). Mechanism 1's backup gives the one recovery point,
    //          which is what the version-upgrade write needs. If a future
    //          patch re-reaches mechanism 2 for characters, this becomes an
    //          accepted gap again rather than parity.
    // note:    The hash is RECOMPUTED, as the game recomputes it, rather than
    //          carrying SaveFileEnvelope.Hash through. That is what makes R1 a
    //          real test of the hash path instead of a tautology.
    // note:    TrailingBytes is ours and has no counterpart: the game writes
    //          nothing after the hash. Re-emitted verbatim so a file that does
    //          carry a tail still round-trips (rule R3).
    // note:    internal, not public. The write path is widened per field,
    //          deliberately, never wholesale; internal + InternalsVisibleTo
    //          makes exactly who can reach it (Norn.Tests and Norn.Adapter)
    //          a compile-time fact rather than a convention. Norn.UI
    //          still cannot reach it — it cannot see GameCore at all — so only
    //          Adapter's own public surface (CharacterEditor) controls what's
    //          reachable from there on.
    /// <summary>Writes the file frame back. Called by <c>Norn.Adapter.CharacterEditor.Save()</c>
    /// and by the round-trip harness; see the note above.</summary>
    internal void SavePlayerToDisk(SaveFileEnvelope envelope)
    {
        ZPackage zpackage = new ZPackage(envelope.PlayerData);
        byte[] array = zpackage.GenerateHash();
        byte[] array2 = zpackage.GetArray();

        using FileStream fileStream = File.Create(m_filename);
        using BinaryWriter binary = new BinaryWriter(fileStream);

        binary.Write(array2.Length);
        binary.Write(array2);
        binary.Write(array.Length);
        binary.Write(array);
        binary.Write(envelope.TrailingBytes);
    }
}
