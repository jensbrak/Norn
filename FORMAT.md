# The Valheim `.fch` character save format

This is a working reference for the character save format, derived by reading
decompiled game code and cross-checked against real save files. It exists so
that absorbing a Valheim patch is a mechanical diff rather than a
re-investigation.

**No game source is reproduced here.** This document restates *structure*:
field order, types, version gates, and the behaviours a parser has to know. It
is a description of a file format, not a copy of anyone's code.

Reference version below is the format as of Valheim **1.0.7**. Historical
values (0.221.4/0.221.10, pre-1.0) are given alongside where they differ, since
an older save can still show up and still has to load correctly.

See [ARCHITECTURE.md](ARCHITECTURE.md) for how Norn mirrors this, and
[CONTRIBUTING.md](CONTRIBUTING.md) for the patch-day workflow that uses it.

---

## 1. Version constants

There are **five independent versions** on the character-save path (plus the
world file's own version, briefly noted at the end of this section), each a
little-endian `Int32` at offset 0 of its own region. **The file envelope
itself carries no version.**

| Region | Where the version lives | Pre-1.0 (0.221.10) | 1.0.7 | Named (1.0.7) |
|---|---|---:|---:|---|
| Profile (outer package) | first `Int32` of the package | 43 | **46** | `Player.DeepNorth` |
| Player data (inner blob) | first `Int32` of the player-data blob | 29 | **33** | `PlayerData.ChunkedNorth` |
| Inventory (inside player data) | first `Int32` of the inventory region | 106 | **109** | `Item.ChunksNCheats` |
| Skills (inside player data) | first `Int32` of the skills region | 2 | **2** (unchanged) | — |
| Map data (per-world blob) | first `Int32` of the blob | 8 | **8** (unchanged) | `Map.PinsAuthor` |

As of 1.0.7 these are typed enum members in source (`Version.Player`,
`Version.PlayerData`, `Version.Item`, `Version.Map`), not loose integer
constants — the numbers above are what actually reaches the wire, and are
what a parser has to compare against regardless of how source spells them.

Other constants on this path:

- **Oldest forward-compatible profile version = 27** — the lower bound of the
  accepted range. Unchanged by 1.0.7.
- **Profile version that moved first-spawn = 40.** Unchanged.
- **Player-data version that moved first-spawn = 28.** Unchanged.

The compatibility check is applied **only to the profile envelope version**,
never to player-data, inventory, skills, or map versions:

```
accepted  ⇔  27 ≤ profileVersion ≤ 46      (43 pre-1.0)
```

**Both bounds are inclusive.**

**World files (`.fwl`/`.fwl2`) carry their own, separate version**, unrelated
to the four above — 37 pre-1.0, **41** at 1.0.7 (`World.DeepNorth`), same
27-style compatibility check (`9 ≤ worldVersion ≤ 41`). Out of scope for the
rest of this document, which is about the character-save path specifically;
noted here only because the world file's version lives in the same style of
region header.

### Numbers that look like versions but are counts

Two write-side literals are easy to mistake for version constants. The reader
always takes counts from the file, never from these:

- **205** (105 pre-1.0) — the number of stat entries per difficulty slot,
  written before each slot's stat float array. Equals `PlayerStatType.Count`.
- **43** — a `GlobalKeys` loop bound the profile writer uses to rebuild
  `knownWorldKeys`, unrelated to any version. Unchanged by 1.0.7.

## 2. File envelope

No magic number, no signature, no compression, and no padding at file level.
The file is a bare frame written directly by a binary writer. **The file is not
itself a package — it contains one.** Unchanged by 1.0.7.

```
offset 0                     Int32                 payloadLength
offset 4                     byte[payloadLength]   payload (the profile package)
offset 4 + payloadLength     Int32                 hashLength
offset 8 + payloadLength     byte[hashLength]      hash
EOF at 8 + payloadLength + hashLength
```

Both length prefixes are signed little-endian `Int32`. **A byte-exact reader
must honour the stored lengths** rather than assuming a 64-byte hash or
deriving lengths from the file size.

### The integrity hash is never checked

The hash is **SHA-512, unkeyed** (not HMAC), computed over exactly the
`payloadLength` payload bytes. It therefore covers the payload's own leading
version `Int32`, and excludes both envelope length prefixes and the hash field
itself.

**The game never verifies it on load** — it reads the trailing bytes and
discards the result. A file with a corrupt hash still loads normally. A
round-trip writer must either recompute the hash or carry the original bytes
through, because the game gives no signal either way.

### Writing

The write path emits payload length, payload, hash length, hash, then flushes.
It writes to `<name>.fch.new` and atomically swaps it over `<name>.fch`,
keeping the displaced file as `<name>.fch.old`. The swap is filesystem
choreography, not format — nothing wraps or transforms the bytes, so the
on-disk file is byte-identical to what the writer produced.

Character saves are **never chunked** — the multi-file `_main.<N>.fwl2` +
`.db2`/`.chunks`/`.chunk` layout 1.0 introduced applies to *world* files only.
A `.fch` is always the single-file frame above, at every version.

### Related

World metadata (`.fwl`, or `.fwl2` from 1.0 on) uses the same frame **minus the
hash** — length, payload, EOF. The trailing hash pair is specific to character
saves. This part of the frame is unchanged by 1.0 despite the file extension
changing; what moved is file *discovery* (1.0 worlds live one-per-directory
under `worlds`/`worlds_local` rather than as flat files) and one new trailing
data block inside the payload — both out of this document's character-save
scope.

### Save directory

The character folder is `characters_local` for a local save source and
`characters` for every other source. **Local is the special case, not the
default** — worth noting, since the folder names suggest the opposite.
Unchanged by 1.0.

## 3. Package wire encoding

Standard .NET binary primitives: little-endian throughout; strings are a
7-bit-encoded-integer length prefix followed by UTF-8 bytes. Unchanged by
1.0.7 — everything below still applies exactly as written.

| Read | Bytes |
|---|---|
| bool | 1 |
| byte / sbyte | 1 |
| short / ushort | 2 |
| int / uint | 4 |
| long / ulong | 8 |
| float | 4 |
| double | 8 |
| string | varint length + UTF-8 |
| `Vector3` | 3 floats, order x, y, z |
| `Vector2i` | 2 int32, order x, y |
| `Vector2s` | 2 int16, order x, y |
| `Quaternion` | 4 floats, order x, y, z, w |
| `ZDOID` | int64 + uint32 (12 bytes) |
| byte array | int32 length + bytes |
| byte array, fixed length | *n* raw bytes, **no length prefix** |
| nested package | int32 length + bytes |
| compressed package | int32 length + bytes, through gzip |

### 3a. Parser behaviours that turn bugs into silent wrong answers

These are semantics of the buffer, not of the format, but each one can turn a
parse mistake into a plausible-looking wrong result rather than an error.
Unchanged by 1.0.7 — 1.0.7 added new, purely additive package methods
(compression/decompression helpers used elsewhere, small-rotation read/write)
that don't touch anything below.

- **One stream, one position, shared by reader and writer.** Writing after
  reading continues at the read cursor and **overwrites in place** — it does
  not append.
- **Byte-array reads short-return at end of stream instead of throwing.** A
  **truncated file therefore parses quietly**, where a scalar read would have
  thrown. This asymmetry is the single most important behaviour to defend
  against: check that the frame accounts for the whole file, or truncation
  looks like success.
- **There are no guards anywhere.** No maximum package size, no sanity check on
  any length prefix, no range validation. A corrupt `Int32` length reaches the
  allocator unchecked.
- **Size is total length, not bytes remaining**, and there is no
  end-of-stream accessor — a parser that wants one must compare position
  against size itself.
- **Reader and writer are constructed without an explicit encoding**, so string
  encoding is the host runtime default. Passing an explicit encoding is a
  divergence, not a fix.
- **Any non-zero byte reads as `true`, and `true` writes as `1`.** A file
  containing a bool byte of `2` would not round-trip byte-identically. No
  game-written file should contain one.

## 4. Profile load order

The game wraps its entire profile load in a single try/catch that **swallows
every exception and still reports success**. A truncated or corrupt file yields
a partially populated profile that the game treats as loaded. A stricter reader
is a deliberate divergence, not a bug fix. Unchanged by 1.0.7.

With `ver` = profile version:

1. `Int32` — profile version.
2. Compatibility check; out-of-range aborts the load.
3. `ver != 46` (`ver != 42` pre-1.0) sets a "back up before saving" flag.
   **Write-side only**, no read effect. Note this is an inequality, not a
   comparison.
4. **Statistics block — restructured at 1.0.7, and not a simple version bump.**
   See section 4a below; it replaces the single flat array pre-1.0 had.
5. `ver ≥ 40` — `bool` first-spawn. Below that the field keeps its default of
   `true` and is corrected by the migration in step 12.
6. **World data.** `Int32` count, then per entry, keyed by an `Int64` world ID:

   | Field | Type | Gate | Default when absent |
   |---|---|---|---|
   | have custom spawn point | bool | — | — |
   | spawn point | `Vector3` | — | — |
   | have logout point | bool | — | — |
   | logout point | `Vector3` | — | — |
   | have death point | bool | `ver ≥ 30` | `false` |
   | death point | `Vector3` | `ver ≥ 30`, same branch | zero |
   | home point | `Vector3` | ungated, after the death pair | — |
   | map-data present flag | bool | `ver ≥ 29`, short-circuit | flag **not read** below 29 |
   | map data | byte[] | present flag true | none |

   Entries are inserted such that a **duplicate world ID throws** — which the
   outer catch then swallows. Unchanged by 1.0.7.
7. `string` — player name.
8. `Int64` — player ID.
9. `string` — start seed.
10. **Metadata, gated `ver ≥ 38`:** `bool` used-cheats → `Int64` Unix seconds
    for the creation date (**time-of-day discarded**, see section 8) →
    **`ver < 46 && ver ≠ 44`**: the three legacy string→float maps in order
    **known worlds, known world keys, known commands**, each an `Int32` count
    followed by count pairs, landing in the statistics block's slot 0 — then
    **nested at `ver ≥ 42`**, three more maps of the same shape: **enemy
    stats, item pickup stats, item craft stats**, also slot 0. At `ver ≥ 46`
    or `ver == 44`, these six maps were already read earlier, inside the
    statistics block itself (section 4a) — this step reads nothing for them. Below
    `ver 38` nothing is read here at all, the creation date becomes a fixed
    literal date, and every dictionary this step could populate stays empty.
11. **Inner player blob, ungated:** `bool` present flag; if true a byte array
    holding the player data.
12. **Migration for `ver < 40`:** re-reads the head of the player blob in a
    throwaway package to recover the first-spawn flag. Consumes no bytes from
    the real parse.

### 4a. Statistics block — restructured at 1.0.7, non-monotonic gate

Pre-1.0 this was one flat `Int32` count + count×`float` array, unconditionally
at `ver ≥ 38`. **1.0.7 replaces it with a 2-D structure — ten difficulty
slots, each carrying nine collections, not just the stat array** — and the
gate that selects the new shape **excludes one version that sits inside its
own range**:

```
ver ≥ 46 || ver == 44   →  new 2-D block (below)
else if ver ≥ 38        →  legacy flat array, into slot 0 only
else if ver ≥ 28        →  legacy 4 ints, into slot 0 only
else (ver == 27)        →  nothing; all stats default to 0
```

**Version 45 is deliberately excluded and falls through to the legacy flat
array**, even though both its neighbours (44 and 46) use the new block. The
chronology: 44 introduced the new layout, 45 reverted to the old one, 46
reinstated the new one — a real, source-confirmed non-monotonicity, not a
typo to "fix" in a mirror.

**New block, gate `ver ≥ 46 || ver == 44`:**

1. `Int32` — stat count per slot (writer emits **205**, was 105 pre-1.0;
   equals `PlayerStatType.Count`).
2. `Int32` — slot count (writer emits **10**).
3. For each slot `i` in `0..slotCount-1`, indexed by `DifficultyRequirement`
   (`RawStats`=0, `Any`=1, `Hammer`=2, `Casual`=3, `VeryEasy`=4, `Easy`=5,
   `Default`=6, `Hard`=7, `VeryHard`=8, `Hardcore`=9):
   1. `statCount` × `float` — this slot's stat array.
   2. Known worlds: `Int32` count + count×(string, float).
   3. Known world keys: `Int32` count + count×(string, float).
   4. Known commands: `Int32` count + count×(string, float).
   5. Enemy stats — `Int32` sub-count (writer emits **5**), then that many
      `Int32` count + count×(string, float) dictionaries, indexed by
      `KillModifiers` (`MixedAndTotal`=0, `Unarmed`=1, `Magic`=2, `Ranged`=3,
      `Melee`=4).
   6. Item pickup stats: `Int32` count + count×(string, float).
   7. Item craft stats: `Int32` count + count×(string, float).
   8. Pickable stats — **new at 1.0.7**: `Int32` count + count×(string, float).
   9. Food-eaten stats — **new at 1.0.7**: `Int32` count + count×(string, float).
   10. Pieces-placed stats — **new at 1.0.7**: `Int32` count + count×(string, float).

Note the **payload position also moved**: pre-1.0, the six original
dictionaries were read late (after player name/ID/seed/used-cheats/date, step
10 above). At `ver ≥ 46`/`ver == 44` they're read here instead, as part of the
statistics block, **before** the world-data count (step 6). Step 10's own
description above states exactly which versions still read them at the old
position.

`PlayerStatType` itself grew alongside this: index range 0–104 is unchanged in
name and order (existing saves' stat indices stay valid), but `Count` moved
105 → 205, a new `None` sentinel was added at 206, and **100 new members**
occupy indices 105–204 — mostly per-death-cause, per-craft-category,
per-harvest, exploration, and building-progress counters. Several of the new
members correlate with achievement-style progress (exploration, boss kills,
treasure finds, survival streaks) and route through the game's own
achievement-eligibility check on write. **Norn does not open any of these for
editing** — noted here as a format fact, not a recommendation either way.

### Dictionary enumeration order is a byte-identity constraint

The world-data block and every dictionary in the statistics block are written
by iterating a dictionary. **.NET does not contract dictionary enumeration
order.** It happens to be insertion order while no removals have occurred, but
that is an implementation detail, not a guarantee, and it is not stable across
runtimes.

A reader that stores these in a plain dictionary and a writer that
re-enumerates it **cannot guarantee byte identity**, and the failure would be
intermittent — green on one machine, red on another, looking exactly like a
parser bug. On-disk order must be preserved explicitly: an ordered list of
key/value pairs, with a lookup beside it if one is wanted.

This applies to world data, every dictionary in the statistics block, known
stations, and item custom data — every dictionary on the path.

## 5. Player data load order

The inner player blob is **the only nested package on the path** — a plain
length-prefixed byte array, uncompressed, independently versioned. It carries
**no compatibility check of its own**: no floor, no ceiling, no logging.
Unchanged by 1.0.7 except where noted.

With `v` = player-data version:

1. `Int32` — player-data version.
2. `v ≥ 7` — float, max health.
3. **Ungated** — float, health. Sanitised: values `≤ 0`, above max, or NaN are
   replaced by max health.
4. `v ≥ 10` — float, used for **both** max stamina and current stamina. When
   absent both default to 100.
5. `8 ≤ v < 28` — bool, **discarded** (legacy first-spawn; recovered separately
   by the profile-level migration).
6. `v ≥ 20` — float, time since death.
7. `v ≥ 23` — string, guardian power. `v ≥ 24` — float, its cooldown.
8. `v == 2` (**exact equality**) — a 12-byte ID, **discarded**.
9. **Inventory**, inline — see section 6, substantially rebuilt at 1.0.7.
10. **Ungated** — known recipes: count + strings.
11. **Known stations:** `v < 15` reads count + strings and **discards them
    all**; otherwise count + (string, `Int32` level) pairs, inserted such that a
    duplicate throws.
12. **Ungated** — known materials: count + strings.
13. **Shown tutorials, gated `v < 19 || v ≥ 21`** — count + strings.
    **Versions 19 and 20 have no tutorials region at all.** This is the least
    intuitive gate in the format.
14. `v ≥ 6` — uniques. `v ≥ 9` — trophies. Both count + strings.
15. **Known biomes — wire type changed at 1.0.7, and the gate is
    non-monotonic:**

    ```
    v ≥ 33 || v == 31   →  count + strings (below)
    else if v ≥ 18       →  count + int32 each, cast to a biome bitmask,
                             migrated to the string form on read (see section 8)
    ```

    Version **32 is excluded**, same shape as section 4a's profile-level gate — 31
    introduced the string form, 32 reverted, 33 reinstated it. Pre-1.0
    (`v ≥ 18`, top branch never reachable) the field was always the legacy
    int form.
16. `v ≥ 22` — known texts: count + (label, text) pairs. The write side strips
    `U+0016` from both; there is no read-side counterpart.
17. `v ≥ 4` — beard string, then hair string.
18. `v ≥ 5` — skin colour `Vector3`, then hair colour `Vector3`. Both default
    to white when absent.
19. `v ≥ 11` — `Int32` model index.
20. **Foods, gated `v ≥ 12`:** count, then per entry —
    - `v ≥ 14`: name string, then **`v ≥ 25`** one float (remaining time),
      **else** one float (health) plus, at `v ≥ 16`, one float (stamina). So
      **v14–15 carry 2 fields, v16–24 carry 3, v ≥ 25 carry 2.**
    - `v` 12–13: a string plus six floats, plus a seventh at `v ≥ 13`, all
      discarded.
21. `v ≥ 17` — **skills**, inline. Unchanged at 1.0.7 — see section 6.
22. **`v ≥ 26`** — a single gate covering four consecutive reads: custom data
    (count + string pairs), then current stamina, then max eitr, then current
    eitr.
23. **Migration at `v < 27`, consuming no bytes:** two known-material entries
    are renamed. Because this is done as a remove-then-add on a set, **it
    reorders the collection** — a real byte-identity hazard for old files. A
    round-trip writer must not apply it.
24. **New at 1.0.7 — trailing build-UI blob, gated `v ≥ 33 || v == 31`** (same
    non-monotonic gate as known biomes, step 15): a length-prefixed byte array
    with **no version field of its own**, read as the *last* thing in the
    player blob, after the flametal migration. See section 5a. `v == 0` (an empty
    blob) is legal and means "reset to defaults."

### Write-side asymmetry worth knowing

The writer emits max stamina only; the reader seeds *both* max and current from
that one value, and the real current stamina is overwritten later by the
`v ≥ 26` block. This is by design, not a bug. Unchanged by 1.0.7.

### Health has no backing field in the game

Max health and current health are not stored in ordinary fields anywhere in the
game's player classes — they are written into a live networked key/value store.
A standalone reader has no such store, so a mirror must invent fields purely as
a destination for bytes that genuinely exist on the wire but that the game's own
model has nowhere wire-facing to put. Unchanged by 1.0.7.

One consequence: for very old files (`v < 7`), the game's health sanitiser falls
back to a value serialised in a Unity prefab, which has no representation in
decompiled code at all. That case is not resolvable from source.

### 5a. Build-UI blob — new nested package, no version field

Positional only — the bytes carry no version of their own. Two sub-blocks,
read in order:

**Recent piece list:**
1. `Int32` count.
2. count × length-prefixed `byte[]`, each decoded as **UTF-8**.

**Favorite piece list:**
1. `Int32` tag count.
2. tag count × length-prefixed `byte[]` (UTF-8).
3. `Int32` piece count.
4. per piece: length-prefixed `byte[]` (UTF-8 name), `Int32` tag-id count,
   then that many `Int32` values.

**These string reads are not the package's own `string` primitive.** They are
plain `Int32` length + UTF-8 bytes, the same shape as a byte array elsewhere in
this format — not the 7-bit-encoded-length form section 3's table describes for
`string`. Do not conflate the two when implementing this block.

## 6. Inventory and skills

Both are read **inline in the same stream**, not as separate nested packages.

### Inventory — replaced, not extended, at version 108

**This is the largest structural change 1.0.7 makes anywhere on the character
path.** `Int32` item-data version first, then the read branches on it:

```
version ≥ 108   →  ushort item count, then the compact bitfield record below
version < 108    →  Int32 item count, then the legacy record (unchanged, see below)
```

Note the **count field width itself changes** — `ushort`, not `Int32` — at the
same point the record layout changes.

**Compact record, `version ≥ 108`.** Unconditional prefix, in order:

| # | Type | Field |
|---|---|---|
| 1 | `Int32` | durability × 100, truncated (see below) |
| 2 | `byte` | grid X |
| 3 | `byte` | grid Y |
| 4 | `byte` | world level |
| 5 | `byte` | flag byte |

Flag byte bits, each gating a conditional read, **in this exact order**
(quality, stack, variant, crafter ID, crafter name, prefab hash, custom-data
count) — then, unconditionally after all eight, count × (string, string) for
custom data:

| Bit | Meaning | Read when set | Default when clear |
|---|---|---|---|
| `0x01` | picked up | (the bit itself) | `false` |
| `0x02` | equipped | (the bit itself) | `false` |
| `0x04` | quality present | `ushort` | **1** |
| `0x08` | stack present | `ushort` | **1** |
| `0x10` | variant present | `Int32` | **0** |
| `0x20` | crafter present | `long` then `string` | **0**, **`""`** |
| `0x40` | prefab hash present | `Int32` | **0** (item dropped) |
| `0x80` | custom-data count present | varint count | **0** |

Then, gated **`version ≥ 109 || version == 107`**: one `byte`, whose bit
`0x01` is a new `cheated` flag (the remaining seven bits are unused). Only
reachable in practice via `version ≥ 109` — see the three-way item-version
split below.

**The prefab identifier is now a one-way hash, not a name.** The compact
record stores a stable hash of the prefab name (a 32-bit hash over the
string, deterministic but not reversible) instead of the name itself, present
only when bit `0x40` is set. **The original name cannot be recovered from a
save file alone.** A parser can still round-trip the hash opaquely, but
displaying or editing an item's real name now requires a separately-sourced
hash→name table built from the game's own asset data — a save file no longer
carries enough information on its own.

**Value migrations on the compact record, all truncating (not rounding), all
on write:**

- Durability: `(int)(value * 100f)` written, `value * 0.01f` read back —
  **this genuinely does not round-trip for most fractional values** (measured
  across 10,000,000 sample values: ~22.7% fail to round-trip byte-identically;
  whole-point durability values are always safe). This is the game's own
  behaviour — Valheim's own save/load cycle drifts a partially-worn item's
  durability downward by hundredths across repeated saves, settling after
  two or three cycles. Not a bug to work around; see section 8.
- Grid position, world level: truncated to `byte` on write.
- Quality, stack: truncated to `ushort` on write.
- Item count itself: truncated to `ushort` on write.

**Legacy record, `version < 108`** — unchanged from before 1.0.7. `Int32` item
count, then per item, in order: prefab name (string), stack (int32),
durability (float), grid position (`Vector2i`), equipped (bool), quality
(int32), variant (int32), crafter ID (int64), crafter name (string), custom
data (count + string pairs), world level (int32), picked up (bool), and, at
**exactly `version == 107`**, one trailing `bool` — a legacy, single-bit form
of the same `cheated` flag the compact record's flag byte carries at bit
`0x01`.

The reader branches on **exact equality with 106** as a fast path within this
legacy form; the other branch reads the identical sequence with the late
fields individually gated:

| Field | Gate | Default |
|---|---|---|
| quality | `≥ 101` | `1` |
| variant | `≥ 102` | `0` |
| crafter ID + name | `≥ 103` (one shared gate) | `0`, `""` |
| custom data | `≥ 104` | empty |
| world level | `≥ 105` | `0` |
| picked up | `≥ 106` | `false` |

**After reading, either form, the game mutates what it read:** items whose
prefab cannot be resolved are dropped; stack counts are clamped to the prefab
maximum; world level is truncated through a byte cast (300 becomes 44,
unchanged from pre-1.0, now also the on-wire representation in the compact
form rather than just a post-read mutation); grid positions may be relocated
or merged by placement logic. An item with an unresolvable prefab (empty name
pre-108; hash `0`, or a hash the game can't resolve, at 108+) is skipped after
its bytes are consumed either way.

### Skills — unchanged at 1.0.7

`Int32` version, then `Int32` count, then per entry: `Int32` skill ID, float
level, and at version `≥ 2` a float accumulator (else zero).

**Entries whose ID is not a defined skill are parsed and then discarded** — the
bytes are consumed either way. `Skills.SkillType` itself is also unchanged —
same members, same values, same gap.

## 7. Map data blob

Each world entry may carry a map blob. It is independently versioned and is
produced and consumed at world-load time rather than on the profile load path.
**Unchanged by 1.0.7** except for one new pin type (noted below) — the map
version ceiling stayed at 8 across the patch, confirmed rather than assumed.

With `m` = map version:

1. `Int32` — map version.
2. **`m ≥ 7`** — the remainder is a **gzip-compressed** sub-package (length +
   bytes). Below 7 the remainder is read inline, uncompressed.
3. `Int32` — texture size. In the game, a mismatch against the live texture size
   aborts the rest of the load.
4. **Exploration masks.** `m ≥ 5` — two raw byte arrays, each `textureSize²`
   bytes, read **without a length prefix**: explored, and explored-by-others.
   Below 5, one bool per texel and no second array.
5. **Pins, gated `m ≥ 2`** — count, then per pin: name (string), position
   (`Vector3`), type (`Int32`), then `m ≥ 3` checked (bool, short-circuit,
   default false), `m ≥ 6` owner ID (int64, default 0), `m ≥ 8` author (string,
   default empty).
6. `m ≥ 4` — trailing bool, public reference position.

**Never hard-code the texture size.** The shipped value is a Unity-serialised
prefab field, not a source constant — the value compiled into source is only a
default. Confirmed shipped value **2048**, i.e. 2048×2048 = 4,194,304
bytes per mask and roughly 8.4 MB for both, which compresses to kilobytes on
disk but expands to megabytes in memory. A future patch could re-tune it, so a
parser must size from the blob's own stored value.

A **missing blob genuinely means "nothing explored"** — the masks are allocated
all-false before any profile is consulted, and there is no special-case code.
A world entry with a null blob and zeroed points is a normal, frequent shape,
not a corruption signal. Note one asymmetry: the game's check is a *null* test
only, so a non-null zero-length array would read past end of stream. A decoder
should treat null and empty as the same case.

**Pin type gained one member at 1.0.7:** `Memorial`, appended after the
existing list — purely additive, no renumbering of anything below it.

### Why this region is not re-encoded

The compression is **gzip (RFC 1952)** — header and CRC32 footer included, not
raw deflate and not zlib.

Knowing the algorithm does not make the region reproducible. **Gzip output is
not stable across runtimes:** the bytes depend on the deflate implementation, on
how the chosen compression level maps to deflate parameters, and on the gzip
header's OS and timestamp fields. The game runs on a different runtime than a
typical tool will, and .NET has changed its deflate backend between versions.
**Recompressing a blob you decompressed will not reproduce the original bytes.**

The only sound treatment is to hold the blob opaque and re-emit it verbatim.
Decompressing it *for display* is fine; re-emitting from the decompressed form
never is.

## 8. Lossy reads — the game's own load is not information-preserving

This matters when reconstructing from an in-memory model rather than carrying
raw bytes through.

1. Inventory items with an unresolvable prefab are dropped.
2. Inventory stack counts are clamped to the prefab maximum.
3. Inventory world level is truncated through a byte cast.
4. Inventory grid positions may be relocated or merged.
5. Foods with an unresolvable prefab are dropped.
6. Skills with an undefined type are dropped.
7. Known stations below `v 15` are read and discarded.
8. Legacy foods at `v` 12–13 are read and discarded.
9. The `v == 2` ID and the `8 ≤ v < 28` legacy first-spawn bool are discarded.
10. Food eitr exists in memory but is never written.
11. Health is silently replaced by max health when out of range or NaN.
12. Known texts are stripped of `U+0016` on write.
13. The `v < 27` material rename **reorders a set**, consuming no bytes.
14. **New at 1.0.7 — item durability is quantised to hundredths on write**
    (`version ≥ 108`), a truncating cast that genuinely does not round-trip
    for most fractional values (section 6). This is the game's own real, deliberate
    lossiness, faithfully replicated rather than worked around.
15. **New at 1.0.7 — known biomes migrate from an int bitmask to a string
    name on read**, at `v ≥ 33 || v == 31` for a save whose stored value is
    still the legacy int form. This migration is **not optional to skip**
    the way most migrations on this path are: the current writer only ever
    emits the string form, so a legacy value that isn't converted has nowhere
    left to go on write.
16. **New at 1.0.7 — an inventory item's prefab identity is a one-way hash**
    at `version ≥ 108`, not a name. The original name is not recoverable
    from the save alone; see section 6.

### Discarded is not the same as skippable

Items 7 and 9 sit *before* other fields in the read sequence. At any layer where
the writer re-emits the **original** version rather than always upgrading,
their bytes must still be captured and replayed verbatim, or every subsequent
field misaligns on write. Where the writer always emits the current version,
those read gates can never be satisfied on write and the values really can be
discarded.

Item 13 is safe to skip outright either way, because it consumes no bytes.

Item 15 is the one genuine exception to that pattern on this whole path: even
a writer that always emits the current version still has to *do* the
migration (convert, not just discard), because the field has to be written in
some form and the current format only has one.

### The creation-date field does not round-trip in the game itself

This is a genuine game bug, not a decompilation artefact. The date is read from
Unix seconds with **time-of-day discarded**. The write side then reconstructs
the integer from that date-only value using a conversion that treats an
unspecified-kind timestamp as **local** time. On any host not on UTC this
recomputes a *different* Unix timestamp than the one on disk, drifting by a
calendar day around local midnight. Unchanged by 1.0.7.

**The game's own save/load cycle does not round-trip this field on a non-UTC
host.** A byte-exact writer must capture the on-disk integer and re-emit it
unchanged rather than recomputing it.

## 9. Consolidated gate inventory

`ver` = profile version, `v` = player-data version, `iv` = inventory version,
`sv` = skills version, `m` = map version.

**This is the fastest place to see what a version bump actually affects.**

| Region | Gate | Note |
|---|---|---|
| profile accepted | `27 ≤ ver ≤ 46` (43 pre-1.0) | both inclusive |
| backup-before-save flag | `ver != 46` (`!= 43` pre-1.0) | inequality; write-side only |
| stats — new 2-D block | `ver ≥ 46 \|\| ver == 44` | **non-monotonic; 45 excluded** |
| stats — legacy flat array | `38 ≤ ver < 46 && ver != 44` | into slot 0 only |
| stats — legacy 4 ints | `28 ≤ ver < 38` | into slot 0 only |
| stats absent | `ver == 27` | |
| legacy metadata dicts (knownWorlds/Keys/Commands) | `ver < 46 && ver != 44`, inside `ver ≥ 38` | position unchanged; content now lands in slot 0 |
| legacy enemy/pickup/craft stats | `ver ≥ 42`, nested in the above | slot 0 only |
| first spawn | `ver ≥ 40` | default `true` |
| death point | `ver ≥ 30` | |
| map-data flag | `ver ≥ 29` | short-circuit; bool unconsumed below |
| max health | `v ≥ 7` | |
| max stamina | `v ≥ 10` | |
| legacy first-spawn bool | `8 ≤ v < 28` | discarded |
| time since death | `v ≥ 20` | |
| guardian power / cooldown | `v ≥ 23` / `v ≥ 24` | |
| legacy ID | `v == 2` | exact equality |
| known stations as pairs | `v ≥ 15` | else strings, discarded |
| shown tutorials | `v < 19 \|\| v ≥ 21` | **19 and 20 excluded entirely** |
| uniques / trophies | `v ≥ 6` / `v ≥ 9` | |
| known biomes — strings | `v ≥ 33 \|\| v == 31` | **non-monotonic; 32 excluded** |
| known biomes — legacy ints | `18 ≤ v < 31` (effectively) | migrated on read, see section 8 |
| known texts | `v ≥ 22` | |
| beard / hair | `v ≥ 4` | |
| skin / hair colour | `v ≥ 5` | default white |
| model index | `v ≥ 11` | |
| foods block | `v ≥ 12` | |
| food modern layout | `v ≥ 14` | else 1 string + 6(+1) floats |
| food time vs. health | `v ≥ 25` | |
| food stamina | `v ≥ 16` | pre-25 branch only |
| skills | `v ≥ 17` | |
| custom data + stamina + eitr | `v ≥ 26` | one gate, four reads |
| material rename | `v < 27` | consumes no bytes |
| first-spawn back-fill | `8 ≤ v < 28` | consumes no bytes |
| build-UI blob | `v ≥ 33 \|\| v == 31` | **new at 1.0.7; same non-monotonic gate as known biomes** |
| inventory legacy vs. compact | `iv ≥ 108` | field-width change too: `ushort` count, not `Int32` |
| item quality (legacy record) | `iv ≥ 101` | default `1` |
| item variant (legacy record) | `iv ≥ 102` | default `0` |
| crafter ID + name (legacy record) | `iv ≥ 103` | one shared gate |
| item custom data (legacy record) | `iv ≥ 104` | default empty |
| item world level (legacy record) | `iv ≥ 105` | default `0` |
| item picked up (legacy record) | `iv ≥ 106` | default `false` |
| item legacy cheated bool | `iv == 107` | trailing bool, legacy record only |
| item cheated flag (compact record) | `iv ≥ 109 \|\| iv == 107` | effectively `iv ≥ 109`; see section 6 |
| skill accumulator | `sv ≥ 2` | default `0` |
| map compressed | `m ≥ 7` | |
| map masks as byte arrays | `m ≥ 5` | else per-texel bools |
| map pins | `m ≥ 2` | |
| pin checked | `m ≥ 3` | short-circuit |
| pin owner ID | `m ≥ 6` | default `0` |
| pin author | `m ≥ 8` | default `""` |
| public reference position | `m ≥ 4` | |

### A gate can only be tested against a save that exercises it

Any real collection of save files covers a narrow band of versions — typically
whatever the owner happened to play. Gates for versions outside that band are
**untestable**, no matter how many files there are. Those gates need
disproportionate review attention precisely because no test will catch a
mistake in them. **The three non-monotonic gates above (profile stats, known
biomes/build-UI) are exactly this kind of trap** — a gate that isn't a simple
threshold is easy to get subtly wrong in a way an untested version range would
never catch.

## 10. Absences — what the format does not contain

- No player position or rotation. Only per-world spawn, logout, death, and home
  points in the profile envelope.
- No per-character world seed beyond a single start-seed string.
- No inventory width or height — prefab-driven.
- No food eitr value.
- No equipment slot assignment — equipped is one bool per item, and the slot is
  derived at runtime.
- No current eitr or stamina below player-data version 26.
- No checksum verification on load.
- No magic bytes or file signature — the first four bytes are a length.
- **No active status effects of any kind.**
- **No recoverable item name for an inventory item stored at version 108 or
  later** — only a one-way hash of the name (section 6). This is new as of 1.0.7 and
  is a real capability boundary, not just a format curiosity: nothing in a
  `.fch` alone can answer "what item is this" for a save written by 1.0.7+
  without a separately-sourced hash→name table.
