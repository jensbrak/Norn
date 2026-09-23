using Norn.GameCore.Primitives;

namespace Norn.GameCore;

/// <summary>
/// The inner player-data blob (<see cref="PlayerProfile.m_playerData"/>),
/// fully decoded — everything except the permanently-deferred map
/// data (<see cref="PlayerProfile.WorldPlayerData.m_mapData"/>).
/// </summary>
public partial class Player
{
    // note: NOT IN SOURCE. Declaration order below mostly follows Load's read
    // order, confirmed against source for every field except the invented
    // health pair. Two pairs are an exception: m_stamina before m_maxStamina,
    // and m_eitr before m_maxEitr, follow source's own declaration order
    // instead — source declares each pair in that order even though Load
    // reads the max of each pair first.

    // note: NOT IN SOURCE — INVENTED STORAGE. The game does not keep current
    // or max health in an instance field at all. Character.SetMaxHealth/
    // SetHealth write into a live ZDO (a networked key/value store) under
    // ZDOVars.s_maxHealth/s_health; Character.m_health (public float, default
    // 10f) is the prefab's *base* max health, an unrelated value. A
    // standalone reader has no ZDO, so these two floats need somewhere to
    // live — the bytes genuinely exist on the wire, the game's own model just
    // has no wire-facing field for them.
    public float m_maxHealth;

    public float m_health;

    public float m_stamina = 100f;

    public float m_maxStamina = 100f;

    public float m_timeSinceDeath = 999999f;

    public string m_guardianPower = "";

    public float m_guardianPowerCooldown;

    // note: LOCATION DIVERGENCE. Declared on Humanoid in source, not Player.
    // See Inventory.cs.
    public Inventory m_inventory = new Inventory();

    // note: TYPE DIVERGENCE. HashSet<string> in source. Held as an ordered
    // List<string> instead — .NET does not contract HashSet's enumeration
    // order either, the same reasoning as PlayerProfile's Dictionary fields.
    // OrderedCollections.AddToSet reproduces
    // HashSet.Add's silently-drop-duplicate semantics.
    public List<string> m_knownRecipes = new List<string>();

    // note: TYPE DIVERGENCE, same reasoning, for a Dictionary<string, int>
    // whose source insertion is Dictionary.Add (throws on duplicate).
    public List<KeyValuePair<string, int>> m_knownStations = new List<KeyValuePair<string, int>>();

    public List<string> m_knownMaterial = new List<string>();

    public List<string> m_shownTutorials = new List<string>();

    public List<string> m_uniques = new List<string>();

    public List<string> m_trophies = new List<string>();

    // note: TYPE DIVERGENCE, same reasoning, for HashSet<string>.
    // note: WIRE TYPE CHANGED AT 1.0.7. This was HashSet<Heightmap.Biome> in
    // source through 0.221.10, stored as ints; it is HashSet<string> now,
    // stored as strings. Saves below the new gate still carry ints and are
    // migrated on read through BiomeSector.GetBiomeName — see Load.
    public List<string> m_knownBiome = new List<string>();

    // note: TYPE DIVERGENCE, same reasoning, for a Dictionary<string, string>
    // whose source insertion is the indexer (overwrite in place).
    public List<KeyValuePair<string, string>> m_knownTexts = new List<KeyValuePair<string, string>>();

    // note: LOCATION DIVERGENCE. Declared on Humanoid in source (protected
    // string, named m_beardItem/m_hairItem there too), not on Player.
    public string m_beardItem = "";

    public string m_hairItem = "";

    public Vector3 m_skinColor = Vector3.one;

    public Vector3 m_hairColor = Vector3.one;

    public int m_modelIndex;

    // note: VISIBILITY DIVERGENCE. private readonly in source, exposed via
    // GetFoods() returning the live list; made public and non-readonly here.
    public List<Food> m_foods = new List<Food>();

    public Skills m_skills = new Skills();

    // note: TYPE DIVERGENCE, same reasoning, for a Dictionary<string, string>
    // (public, not readonly, in source) whose source insertion is the
    // indexer (overwrite in place).
    public List<KeyValuePair<string, string>> m_customData = new List<KeyValuePair<string, string>>();

    public float m_eitr;

    public float m_maxEitr;

    // note: NOT IN SOURCE. The build-UI blob 1.0.7 appended to the player data
    // is Hud state in the game — Player.Save pulls it from
    // Hud.instance.m_buildUi and Player.Load hands it straight back, so the
    // Player object itself never holds it and has no field for it. Held here
    // so a headless reader can round-trip the bytes it does not decode (R3).
    // Defaults to empty, not null: the write is unconditional, and source's
    // own Hud-less branch writes an empty array rather than skipping it.
    public byte[] m_buildUiData = Array.Empty<byte>();

    // note: NOT IN SOURCE. The game reads `v` into a local variable and never
    // retains it — no field. Captured for the same reason as
    // PlayerProfile.ProfileVersion: it decides which R1/R2 doctrine applies
    // (R1 only when the on-disk version equals the writer version),
    // and the UI's tabs want it too.
    public int PlayerDataVersion { get; private set; }

    // mirrors: Player.Load(ZPackage)
    // source:  Valheim 1.0.15
    // note:    OMISSION, accepted. `m_isLoading` (set true at entry, false near
    //          the end), unequipping all items at entry, and three runtime
    //          refresh calls at the tail are all runtime/UI bookkeeping with no
    //          bearing on the byte format — not carried, matching the pattern
    //          already applied throughout this project (filesystem/UI
    //          choreography is out of scope, not part of the wire format).
    // note:    OMITTED SIDE EFFECT. `SetGuardianPower(string)` is not a
    //          passthrough in source — it also computes a hash, resolves a
    //          StatusEffect from ObjectDB, and (if a live ZoneSystem exists)
    //          calls AddUniqueKey, which inserts the guardian-power name into
    //          m_uniques. m_uniques is read later in this same sequence
    //          (v >= 6), so a live game can end up with an extra entry the
    //          file itself never listed — a runtime hazard, not a format one.
    //          This mirror does only the field assignment; the hash/status
    //          effect/unique-key insertion are unreachable without a live
    //          ObjectDB/ZoneSystem and have no bearing on reading a file.
    // note:    OMITTED SIDE EFFECT. `SetMaxHealth`'s real body re-clamps the
    //          *previous* health value down if it now exceeds the new max —
    //          inert here regardless, since health has not been read yet at
    //          this point in the sequence (it is read immediately after) and
    //          gets overwritten unconditionally on the next line.
    // note:    OMITTED SIDE EFFECT. The sanitized health value is passed to
    //          `SetHealth` in source, whose real body additionally floors a
    //          negative result at 0f after this mirror's own sanitize already
    //          runs. Reachable only if `m_maxHealth` itself is negative (the
    //          sanitize would then substitute that negative value for an
    //          out-of-range health) — the same corrupt-input class as the
    //          `Math.Clamp` risk noted below, and likewise not corpus-tested.
    // note:    The `v >= 10` step passes the read value to `SetMaxStamina`
    //          (which itself clamps the *previous* m_stamina into the new
    //          range) and then separately assigns m_stamina the same value
    //          directly on the next source line — the direct assignment
    //          unconditionally overwrites whatever the clamp produced, so
    //          only the net effect (both fields become the read value) is
    //          reproduced here.
    // note:    OMITTED SIDE EFFECT. `SetMaxEitr`'s real body re-clamps the
    //          *previous* eitr value down if it now exceeds the new max —
    //          inert here regardless, since eitr is read immediately after
    //          and overwritten unconditionally, same reasoning as
    //          SetMaxHealth/SetMaxStamina above.
    // note:    RISK. `Math.Clamp` throws `ArgumentException` when its min
    //          exceeds its max; the game's `Mathf.Clamp` has no such check
    //          and clamps unconditionally. A file carrying a negative
    //          m_maxStamina or m_maxEitr (nothing upstream validates either —
    //          ZPackage performs no bounds checking anywhere) makes this
    //          mirror throw where the
    //          game would silently produce a value. Consistent with this
    //          project's existing stance (see LoadPlayerDataFromDisk's
    //          BEHAVIOURAL DIVERGENCE note): a loud failure on corrupt input
    //          is preferred over silent misrepresentation. Not corpus-tested.
    // note:    RISK. `m_maxHealth` defaults to 0f. For a hypothetical v &lt; 7
    //          file (none in the compatible range any real client would
    //          produce), source's sanitize would compare against
    //          `base.GetMaxHealth()`, which — with no live ZDO for a
    //          standalone reader to consult — falls through to
    //          `Character.GetMaxHealthBase()`: the Player prefab's base max
    //          health, a Unity-serialized value with no representation in
    //          decompiled source (see the note on m_maxHealth's field
    //          declaration). Comparing against 0f instead means any positive
    //          stored health at v &lt; 7 would be replaced by 0f rather than
    //          the game's true default. Unresolvable from code; recorded
    //          rather than guessed at.
    // note:    OMISSION, accepted, three discarded values kept as plain
    //          discards (not captured): the legacy first-spawn bool, the
    //          v == 2 ZDOID, and v &lt; 15's legacy station-name strings. Save
    //          is unconditional and always writes the current player-data
    //          version (matching source exactly), so none of these three
    //          gates (8 ≤ v &lt; 28, v == 2, v &lt; 15) can ever fire on write —
    //          nothing written depends on these values, and capturing them
    //          would be invented state serving no purpose, matching source's
    //          own plain-discard treatment.
    // note:    Migration `v < 27` (flametal rename in m_knownMaterial) is
    //          deliberately NOT applied.
    //          It consumes no bytes and reorders a set that a round-trip
    //          writer must reproduce as read, not as the game would leave it
    //          after loading.
    // note:    Lossy reads are NOT mirrored for foods (ObjectDB prefab
    //          resolution) — every food entry is kept verbatim regardless of
    //          whether the game would drop it, same reasoning as Inventory.
    public void Load(ZPackage pkg)
    {
        Version.PlayerData v = (Version.PlayerData)pkg.ReadInt();
        PlayerDataVersion = (int)v;

        if (v >= Version.PlayerData.MaxHealth)
        {
            m_maxHealth = pkg.ReadSingle();
        }

        float health = pkg.ReadSingle();
        if (health <= 0f || health > m_maxHealth || float.IsNaN(health))
        {
            health = m_maxHealth;
        }

        m_health = health;

        if (v >= Version.PlayerData.MaxStamina)
        {
            float maxStamina = pkg.ReadSingle();
            m_maxStamina = maxStamina;
            m_stamina = maxStamina;
        }

        if (v >= Version.PlayerData.FirstSpawn && v < Version.PlayerData.MovedFirstSpawn)
        {
            pkg.ReadBool();
        }

        if (v >= Version.PlayerData.TimeSinceDeath)
        {
            m_timeSinceDeath = pkg.ReadSingle();
        }

        if (v >= Version.PlayerData.GuardianPower)
        {
            m_guardianPower = pkg.ReadString();
        }

        if (v >= Version.PlayerData.GuardianPowerCooldown)
        {
            m_guardianPowerCooldown = pkg.ReadSingle();
        }

        if (v == Version.PlayerData.Original)
        {
            pkg.ReadZDOID();
        }

        m_inventory.Load(pkg);

        int knownRecipesCount = pkg.ReadInt();
        for (int i = 0; i < knownRecipesCount; i++)
        {
            OrderedCollections.AddToSet(m_knownRecipes, pkg.ReadString());
        }

        if (v < Version.PlayerData.Stations)
        {
            int legacyStationsCount = pkg.ReadInt();
            for (int i = 0; i < legacyStationsCount; i++)
            {
                pkg.ReadString();
            }
        }
        else
        {
            int knownStationsCount = pkg.ReadInt();
            for (int i = 0; i < knownStationsCount; i++)
            {
                OrderedCollections.Add(m_knownStations, pkg.ReadString(), pkg.ReadInt());
            }
        }

        int knownMaterialCount = pkg.ReadInt();
        for (int i = 0; i < knownMaterialCount; i++)
        {
            OrderedCollections.AddToSet(m_knownMaterial, pkg.ReadString());
        }

        if (v < Version.PlayerData.RemoveTutorials || v >= Version.PlayerData.ReAddTutorials)
        {
            int shownTutorialsCount = pkg.ReadInt();
            for (int i = 0; i < shownTutorialsCount; i++)
            {
                OrderedCollections.AddToSet(m_shownTutorials, pkg.ReadString());
            }
        }

        if (v >= Version.PlayerData.Uniques)
        {
            int uniquesCount = pkg.ReadInt();
            for (int i = 0; i < uniquesCount; i++)
            {
                OrderedCollections.AddToSet(m_uniques, pkg.ReadString());
            }
        }

        if (v >= Version.PlayerData.Trophies)
        {
            int trophiesCount = pkg.ReadInt();
            for (int i = 0; i < trophiesCount; i++)
            {
                OrderedCollections.AddToSet(m_trophies, pkg.ReadString());
            }
        }

        // note: NON-MONOTONIC GATE, the same shape as PlayerProfile's
        // statistics gate one level up and excluding the same build. Player-
        // data version 32 (ChunkedSaves) is NOT in the string branch: 31
        // (AbandonedDN) introduced strings, 32 reverted to ints, 33
        // (ChunkedNorth) reinstated them. Profile versions 44/45/46 correspond
        // to player-data versions 31/32/33 respectively. No corpus file exists
        // at either excluded version, so this is carried on source-reading
        // alone — re-read it, do not assume it, on the next patch.
        if (v >= Version.PlayerData.ChunkedNorth || v == Version.PlayerData.AbandonedDN)
        {
            int knownBiomeCount = pkg.ReadInt();
            for (int i = 0; i < knownBiomeCount; i++)
            {
                OrderedCollections.AddToSet(m_knownBiome, pkg.ReadString());
            }
        }
        else if (v >= Version.PlayerData.KnownBiomes)
        {
            int knownBiomeCount = pkg.ReadInt();
            for (int i = 0; i < knownBiomeCount; i++)
            {
                Biome biome = (Biome)pkg.ReadInt();
                OrderedCollections.AddToSet(m_knownBiome, BiomeSector.GetBiomeName(biome));
            }
        }

        if (v >= Version.PlayerData.KnownTexts)
        {
            int knownTextsCount = pkg.ReadInt();
            for (int i = 0; i < knownTextsCount; i++)
            {
                OrderedCollections.Set(m_knownTexts, pkg.ReadString(), pkg.ReadString());
            }
        }

        if (v >= Version.PlayerData.SkinHair)
        {
            m_beardItem = pkg.ReadString();
            m_hairItem = pkg.ReadString();
        }

        if (v >= Version.PlayerData.SkinHairColor)
        {
            m_skinColor = pkg.ReadVector3();
            m_hairColor = pkg.ReadVector3();
        }

        if (v >= Version.PlayerData.PlayerModel)
        {
            m_modelIndex = pkg.ReadInt();
        }

        if (v >= Version.PlayerData.Food)
        {
            m_foods.Clear();
            int foodsCount = pkg.ReadInt();
            for (int i = 0; i < foodsCount; i++)
            {
                if (v >= Version.PlayerData.Food2)
                {
                    Food food = new Food();
                    food.m_name = pkg.ReadString();
                    if (v >= Version.PlayerData.FoodTime)
                    {
                        food.m_time = pkg.ReadSingle();
                    }
                    else
                    {
                        food.m_health = pkg.ReadSingle();
                        if (v >= Version.PlayerData.FoodStamina)
                        {
                            food.m_stamina = pkg.ReadSingle();
                        }
                    }

                    m_foods.Add(food);
                }
                else
                {
                    pkg.ReadString();
                    pkg.ReadSingle();
                    pkg.ReadSingle();
                    pkg.ReadSingle();
                    pkg.ReadSingle();
                    pkg.ReadSingle();
                    pkg.ReadSingle();
                    if (v >= Version.PlayerData.BurnRate)
                    {
                        pkg.ReadSingle();
                    }
                }
            }
        }

        if (v >= Version.PlayerData.Skills)
        {
            m_skills.Load(pkg);
        }

        if (v >= Version.PlayerData.EitrStamina)
        {
            int customDataCount = pkg.ReadInt();
            for (int i = 0; i < customDataCount; i++)
            {
                OrderedCollections.Set(m_customData, pkg.ReadString(), pkg.ReadString());
            }

            float stamina = pkg.ReadSingle();
            m_stamina = Math.Clamp(stamina, 0f, m_maxStamina);

            m_maxEitr = pkg.ReadSingle();

            float eitr = pkg.ReadSingle();
            m_eitr = Math.Clamp(eitr, 0f, m_maxEitr);
        }

        // note: NEW AT 1.0.7, read LAST — after the flametal migration, which
        // consumes no bytes. Same non-monotonic gate as the known-biome read
        // above: 32 is excluded. Source hands the bytes to
        // Hud.instance.m_buildUi.LoadFromBinary and keeps nothing; this mirror
        // has no Hud and no reason to decode them, so they are held opaquely
        // and re-emitted verbatim, per the R3 doctrine already applied to
        // PlayerProfile.m_playerData and WorldPlayerData.m_mapData.
        // The inner layout IS known — two positional sub-blocks (a recent-piece
        // list and a favourite-piece list) with no version field of their own,
        // using ReadByteArray/UTF-8 rather than ReadString. It is
        // deliberately not decoded here: nothing in
        // Norn reads it, and BuildUi is not a load/save type.
        if (v >= Version.PlayerData.ChunkedNorth || v == Version.PlayerData.AbandonedDN)
        {
            m_buildUiData = pkg.ReadByteArray();
        }
    }

    // mirrors: Player.Save(ZPackage)
    // source:  Valheim 1.0.15
    // note:    One writer-only side effect the game performs is NOT
    //          replicated: it strips U+0016 (SYN) from both key and value of
    //          m_knownTexts — a lossy, one-way transform with no read-side
    //          counterpart, so a round-trip writer must reproduce what was
    //          read rather than additionally mangle it. The v < 27 flametal
    //          migration is a separate matter, not a second unreplicated
    //          side effect: the game applies nothing extra on its write path
    //          for it either, and since this mirror's own Load never applies
    //          that migration in the first place (see the note on Load
    //          above), there's nothing here for Save to skip.
    public void Save(ZPackage pkg)
    {
        pkg.Write(33);
        pkg.Write(m_maxHealth);
        pkg.Write(m_health);
        pkg.Write(m_maxStamina);
        pkg.Write(m_timeSinceDeath);
        pkg.Write(m_guardianPower);
        pkg.Write(m_guardianPowerCooldown);

        m_inventory.Save(pkg);

        pkg.Write(m_knownRecipes.Count);
        foreach (string recipe in m_knownRecipes)
        {
            pkg.Write(recipe);
        }

        pkg.Write(m_knownStations.Count);
        foreach (KeyValuePair<string, int> pair in m_knownStations)
        {
            pkg.Write(pair.Key);
            pkg.Write(pair.Value);
        }

        pkg.Write(m_knownMaterial.Count);
        foreach (string material in m_knownMaterial)
        {
            pkg.Write(material);
        }

        pkg.Write(m_shownTutorials.Count);
        foreach (string tutorial in m_shownTutorials)
        {
            pkg.Write(tutorial);
        }

        pkg.Write(m_uniques.Count);
        foreach (string unique in m_uniques)
        {
            pkg.Write(unique);
        }

        pkg.Write(m_trophies.Count);
        foreach (string trophy in m_trophies)
        {
            pkg.Write(trophy);
        }

        pkg.Write(m_knownBiome.Count);
        foreach (string biome in m_knownBiome)
        {
            pkg.Write(biome);
        }

        pkg.Write(m_knownTexts.Count);
        foreach (KeyValuePair<string, string> pair in m_knownTexts)
        {
            pkg.Write(pair.Key);
            pkg.Write(pair.Value);
        }

        pkg.Write(m_beardItem);
        pkg.Write(m_hairItem);
        pkg.Write(m_skinColor);
        pkg.Write(m_hairColor);
        pkg.Write(m_modelIndex);

        pkg.Write(m_foods.Count);
        foreach (Food food in m_foods)
        {
            pkg.Write(food.m_name);
            pkg.Write(food.m_time);
        }

        m_skills.Save(pkg);

        pkg.Write(m_customData.Count);
        foreach (KeyValuePair<string, string> pair in m_customData)
        {
            pkg.Write(pair.Key);
            pkg.Write(pair.Value);
        }

        pkg.Write(m_stamina);
        pkg.Write(m_maxEitr);
        pkg.Write(m_eitr);

        // note: NEW AT 1.0.7, and written UNCONDITIONALLY — there is no version
        // gate on this write, unlike the read. Source branches on whether the
        // Hud singleton exists: with a Hud it writes the live build UI's bytes,
        // without one it writes `new byte[0]`. Neither branch is skippable, so
        // the mirror always emits something here. Re-emitting exactly what was
        // read is what keeps R1; a legacy save that carried no blob at all
        // writes the empty array, which source treats as "reset to defaults"
        // and is what the Hud-less branch would have produced anyway.
        pkg.Write(m_buildUiData);
    }
}
