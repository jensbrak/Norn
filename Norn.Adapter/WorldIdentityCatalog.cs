using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// Norn's own accreting local cache of world UID → name/seed, resolved by
/// reading a player's local <c>.fwl</c> files.
/// Unlike <see cref="SharedItemDataCatalog"/>/<see cref="LocalizationCatalog"/>
/// (read-only against a bundled/override file), this catalog also writes: a
/// UID, once seen, is meant to stay a solid entry even after the world
/// itself is no longer locally present — so
/// <see cref="Refresh"/> merges freshly-scanned identities into the
/// persisted cache and saves it back, rather than only ever reflecting
/// whatever's on disk right now. The cache file is Norn's own bookkeeping,
/// not a Valheim wire format — no mirror-discipline obligation, no
/// <c>game-derived:</c> tag.
/// </summary>
public static class WorldIdentityCatalog
{
    // volatile, not a plain field: Refresh() replaces this reference from a
    // background Task.Run (Norn.UI's own choice) while TryFind() reads it
    // synchronously from the UI thread. The swap itself is already atomic —
    // the risk volatile closes is visibility: without it, a reader on
    // another thread has no guarantee it observes a just-published
    // reference rather than a stale cached one (found in review).
    private static volatile IReadOnlyDictionary<long, WorldIdentityDto> _entries =
        new Dictionary<long, WorldIdentityDto>();

    /// <summary>
    /// The cache file's columns, in write order — one source of truth for
    /// both <see cref="TryWriteCache"/>'s header and
    /// <see cref="TryReadCache"/>'s "is this actually our format" check, so
    /// the two can't drift apart.
    /// </summary>
    private static readonly string[] RequiredColumns =
        ["Uid", "Name", "SeedName", "Seed", "DateAdded", "SeenAsFiles", "ConflictNote"];

    /// <summary>Number of known world identities.</summary>
    public static int Count => _entries.Count;

    /// <summary>Resolves a world UID to its cached identity, or <c>null</c> on
    /// a miss — a handled case (never locally seen), not an error.</summary>
    public static WorldIdentityDto? TryFind(long uid)
        => _entries.TryGetValue(uid, out var dto) ? dto : null;

    /// <summary>
    /// Reads the existing cache at <paramref name="cachePath"/> (empty if
    /// absent — a normal first-run state), reads every <paramref
    /// name="worldFilePaths"/> entry as a <c>.fwl</c> and merges any newly
    /// resolved identity in (first-seen wins per UID; a later differing scan
    /// is recorded as <see cref="WorldIdentityDto.ConflictNote"/> rather than
    /// overwriting; the scanned file's own name is always appended to
    /// <see cref="WorldIdentityDto.SeenAsFiles"/> if new, regardless of
    /// whether it agreed or conflicted), then persists the merged result back to
    /// <paramref name="cachePath"/>. A single unreadable <c>.fwl</c> is
    /// skipped, not fatal — matches <see cref="SharedItemDataCatalog"/>'s own
    /// graceful-degradation contract. If the existing cache file is present
    /// but fails to parse, it is left untouched on disk rather than risk
    /// overwriting data this pass couldn't understand — freshly-scanned
    /// identities are still usable in-memory for this session either way.
    /// </summary>
    public static void Refresh(string cachePath, IEnumerable<string> worldFilePaths)
    {
        var merged = TryReadCache(cachePath, out var cacheWasReadable);

        foreach (var path in worldFilePaths)
        {
            if (!TryReadWorldFile(path, out var name, out var seedName, out var seed, out var uid))
            {
                continue;
            }

            // Not the file stem: a chunked (1.0+) world's file is always
            // "_main.<N>.fwl2", so every world on the machine would be filed
            // under the same meaningless name. The save name is the world
            // directory's name there, and the stem only for a legacy .fwl.
            var stem = WorldFileClassifier.GetWorldSaveName(path);

            if (merged.TryGetValue(uid, out var current))
            {
                var seenAsFiles = current.SeenAsFiles;
                if (!seenAsFiles.Any(f => f.Equals(stem, StringComparison.OrdinalIgnoreCase)))
                {
                    seenAsFiles = [.. seenAsFiles, stem];
                }

                var differs = current.Name != name || current.SeedName != seedName || current.Seed != seed;
                var conflictNote = current.ConflictNote;
                if (differs && conflictNote is null)
                {
                    conflictNote = $"A later scan found this world locally named '{name}' (seed {seedName}) instead.";
                }

                merged[uid] = current with { SeenAsFiles = seenAsFiles, ConflictNote = conflictNote };
            }
            else
            {
                merged[uid] = new WorldIdentityDto(uid, name, seedName, seed, DateTime.UtcNow, [stem], null);
            }
        }

        _entries = merged;

        if (cacheWasReadable)
        {
            TryWriteCache(cachePath, merged);
        }
    }

    private static bool TryReadWorldFile(string path, out string name, out string seedName, out int seed, out long uid)
    {
        name = "";
        seedName = "";
        seed = 0;
        uid = 0;

        try
        {
            var payload = World.ReadPayloadFromDisk(path);
            var world = new World();
            if (!world.Load(payload))
            {
                return false;
            }

            name = world.m_name;
            seedName = world.m_seedName;
            seed = world.m_seed;
            uid = world.m_uid;
            return true;
        }
        catch
        {
            // Corrupt or unreadable .fwl: skip, matching SharedItemDataCatalog's
            // own graceful-degradation contract — one bad file must not sink
            // the whole scan.
            return false;
        }
    }

    private static Dictionary<long, WorldIdentityDto> TryReadCache(string path, out bool readable)
    {
        if (!File.Exists(path))
        {
            readable = true;
            return new Dictionary<long, WorldIdentityDto>();
        }

        var result = new Dictionary<long, WorldIdentityDto>();

        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();

            // A missing/garbage header means this isn't our cache format at
            // all (e.g. some other file, or the header line itself
            // corrupted) — that must still fail the whole read (readable =
            // false) so Refresh() leaves it untouched on disk, per this
            // method's own documented contract. Only once the header is
            // confirmed as ours does a per-row try/catch make sense to skip
            // a single bad *data* row rather than the whole file (below) —
            // conflating the two let a garbage file (wrong header, caught by
            // this check) fall through to the per-row catch instead,
            // silently producing an empty result that then overwrote the
            // original file (a regression caught by
            // WorldIdentityCatalogTests right after introducing the per-row
            // catch — fixed here before it shipped).
            // Every column the row loop below reads, not just "Uid" (found
            // in review): a header like `Uid,Name` passed the narrower
            // check, then every row threw on the first missing field, got
            // swallowed by the per-row catch, and the method still reported
            // readable = true — so Refresh() overwrote a cache it had in
            // fact failed to read, which is the exact outcome both this
            // check and the per-row catch exist to prevent, just reached by
            // a different route.
            if (csv.HeaderRecord is null || RequiredColumns.Any(column => !csv.HeaderRecord.Contains(column)))
            {
                readable = false;
                return new Dictionary<long, WorldIdentityDto>();
            }

            while (csv.Read())
            {
                // Per-row, not just per-file: a single malformed row (partial
                // write, manual edit, disk corruption) must not discard every
                // other valid row in the cache (found in review).
                try
                {
                    var uid = csv.GetField<long>("Uid");
                    var name = csv.GetField<string>("Name") ?? "";
                    var seedName = csv.GetField<string>("SeedName") ?? "";
                    var seed = csv.GetField<int>("Seed");
                    var dateAdded = DateTime.Parse(
                        csv.GetField<string>("DateAdded") ?? "",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind);
                    var seenAsFiles = ParseSeenAsFiles(csv.GetField<string>("SeenAsFiles") ?? "");
                    var conflictNoteRaw = csv.GetField<string>("ConflictNote");
                    var conflictNote = string.IsNullOrEmpty(conflictNoteRaw) ? null : conflictNoteRaw;

                    result[uid] = new WorldIdentityDto(uid, name, seedName, seed, dateAdded, seenAsFiles, conflictNote);
                }
                catch
                {
                    // Skip just this row; keep reading the rest of the cache.
                }
            }

            readable = true;
            return result;
        }
        catch
        {
            readable = false;
            return new Dictionary<long, WorldIdentityDto>();
        }
    }

    /// <summary>
    /// <see cref="SeenAsFiles"/> is written as a JSON string array (found in
    /// review: the previous semicolon-joined form couldn't round-trip a real
    /// filename containing a literal <c>;</c>). Falls back to splitting on
    /// <c>;</c> for a cache file written before this change, so an existing
    /// cache degrades gracefully on first read rather than losing every row
    /// with a non-empty <see cref="WorldIdentityDto.SeenAsFiles"/> at once.
    /// <para>
    /// Null elements are dropped, deliberately: <c>[null]</c> is valid JSON
    /// that deserializes happily into a <c>string[]</c> whose element is
    /// null, regardless of nullable annotations (they aren't enforced during
    /// deserialization). That null then survived into the returned DTO and
    /// blew up much later, at <see cref="Refresh"/>'s
    /// <c>seenAsFiles.Any(f =&gt; f.Equals(...))</c> — outside the per-row
    /// recovery, so one poisoned row aborted the entire refresh (found in
    /// review). Validating on the way in keeps that failure local.
    /// </para>
    /// </summary>
    private static string[] ParseSeenAsFiles(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<string?[]>(raw)?.OfType<string>().ToArray() ?? [];
        }
        catch (JsonException)
        {
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private static void TryWriteCache(string path, Dictionary<long, WorldIdentityDto> entries)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Staged, not written straight to the live path: a process kill
            // mid-write used to leave the live cache file truncated, which
            // TryReadCache then silently treated as totally unreadable —
            // discarding every previously-cached identity with no
            // indication (found in review). Writing to a temp file and only
            // replacing the live one once the write has fully succeeded
            // means an interrupted write never touches the real cache at all.
            var tempPath = path + ".tmp";
            using (var writer = new StreamWriter(tempPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                foreach (var column in RequiredColumns)
                {
                    csv.WriteField(column);
                }

                csv.NextRecord();

                foreach (var entry in entries.Values.OrderBy(e => e.Uid))
                {
                    csv.WriteField(entry.Uid);
                    csv.WriteField(entry.Name);
                    csv.WriteField(entry.SeedName);
                    csv.WriteField(entry.Seed);
                    csv.WriteField(entry.DateAdded.ToString("O", CultureInfo.InvariantCulture));
                    csv.WriteField(JsonSerializer.Serialize(entry.SeenAsFiles));
                    csv.WriteField(entry.ConflictNote ?? "");
                    csv.NextRecord();
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // Best-effort persistence (permissions, disk full, ...) — must
            // not crash the app. Freshly-scanned entries are still usable
            // in-memory for this session via TryFind regardless.
        }
    }
}
