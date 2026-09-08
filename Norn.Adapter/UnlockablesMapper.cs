using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>Maps <see cref="Player"/>'s eight known-recipe/station/material/
/// biome/unique/trophy/text/tutorial fields to <see cref="UnlockablesDto"/>.
/// Every field is sorted ordinal-alphabetically for display — safe because
/// each entry's key is unique (see <see cref="UnlockablesDto"/>'s remarks),
/// so re-ordering for readability never risks losing or conflating an entry.
/// <para>
/// Five of the eight fields resolve their raw wire strings to real English
/// text, each via whichever catalog actually covers
/// that kind of key — confirmed field-by-field against real save data
/// rather than assumed, since the wire's own naming isn't uniform:
/// recipes/material/stations are <c>$</c>-prefixed localization keys
/// (<see cref="LocalizationCatalog"/>); trophies are inventory-item prefab
/// names (<see cref="SharedItemDataCatalog"/>, the same catalog Inventory's
/// grid already uses); known texts carry <em>two</em> localization keys per
/// entry (label and body — the wire never stores literal text directly, as
/// an earlier pass assumed). Sorting happens on the resolved value, not the
/// raw key, now that the resolved value is what a user actually reads.
/// Uniques, known biomes, and shown tutorials stay raw: uniques/tutorials
/// are prefab or event-flag references with no <c>$</c> prefix and (for
/// tutorials) no clean, confirmed mapping onto the localization
/// namespace — a spot check found wire IDs that don't match any loc key at
/// all (<c>sleepingspot</c>) or match a differently-spelled one
/// (<c>temple4</c> on the wire vs. <c>tutorial_stemple4_*</c> in the
/// catalog) — and biomes are already-readable <see cref="Heightmap.Biome"/>
/// enum names with no localization entry to gain from resolving anyway. A
/// resolution miss falls back to the raw key (<see cref="Resolve"/>), same
/// graceful-degradation contract as the catalogs it calls.
/// </para>
/// <para>
/// Two real data-quality issues confirmed against the full corpus, both
/// display-layer fixes only — <see cref="GameCore.Player"/> keeps reading
/// and writing these fields exactly as found (R1/R2), since a raw wire
/// entry — however useless to show — is not this mapper's to alter in the
/// underlying model, only in what gets displayed from it. **Empty-string
/// entries**: 14 of 21 real save files carry a literal zero-length string
/// in <c>m_uniques</c> — common enough to be a genuine, if unexplained,
/// property of the game's own <c>m_uniques</c>/global-key data, not save
/// corruption specific to one file. Rendered as a list item, an empty
/// string is an invisible row that still takes up a row's height — the
/// exact cause of an "extra spacing before the first item" report.
/// Filtered out post-sanitization, every field, not just uniques (cheap,
/// and nothing says the same defect can't recur elsewhere). **Embedded
/// control characters**: two real recipe keys in the corpus carry a
/// leading <c>U+0016</c> (SYN) pair — the same character the game's own
/// writer strips from <c>m_knownTexts</c> on save (never on load) — evidently
/// not unique to that one field. <see cref="Sanitize"/> strips every
/// Unicode control character from a raw key *before* it's used for
/// lookup or shown as a fallback — before, not after: <see cref="Resolve"/>
/// only ever sanitizes its `key` input, never a catalog's returned text,
/// so <see cref="KnownTextDto.Text"/>'s legitimate embedded newlines
/// (paragraph breaks in real body text) are untouched. A side benefit,
/// not just a cosmetic fix: stripping first means the previously-garbled
/// recipe now resolves correctly instead of falling back to a
/// still-partly-raw key.
/// </para>
/// </summary>
public static class UnlockablesMapper
{
    public static UnlockablesDto Map(Player player)
    {
        return new UnlockablesDto(
            ResolvedSorted(player.m_knownRecipes),
            player.m_knownStations
                .Select(pair => new KnownStationDto(Resolve(pair.Key), pair.Value))
                .Where(station => station.Name.Length > 0)
                .OrderBy(station => station.Name, StringComparer.Ordinal)
                .ToList(),
            ResolvedSorted(player.m_knownMaterial),
            Sorted(player.m_uniques),
            player.m_trophies
                .Select(Sanitize)
                .Where(name => name.Length > 0)
                .Select(name => SharedItemDataCatalog.TryFind(name)?.DisplayName ?? name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            player.m_knownBiome
                .Select(biome => biome.ToString())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            player.m_knownTexts
                .Select(pair => new KnownTextDto(Resolve(pair.Key), Resolve(pair.Value)))
                .Where(text => text.Label.Length > 0)
                .OrderBy(text => text.Label, StringComparer.Ordinal)
                .ToList(),
            Sorted(player.m_shownTutorials));
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> values)
        => values.Select(Sanitize).Where(value => value.Length > 0)
            .OrderBy(value => value, StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string> ResolvedSorted(IEnumerable<string> values)
        => values.Select(Resolve).Where(value => value.Length > 0)
            .OrderBy(value => value, StringComparer.Ordinal).ToList();

    private static string Resolve(string key)
    {
        var clean = Sanitize(key);
        return LocalizationCatalog.TryFind(clean) ?? clean;
    }

    /// <summary>Strips every Unicode control character (the observed
    /// <c>U+0016</c> SYN pair, and defensively any other C0/C1 control
    /// character — see this class's own doc comment) from a raw wire
    /// string. Never applied to a catalog's returned text, only to the key
    /// used to look it up or shown as a fallback on a miss. <c>internal</c>,
    /// not <c>private</c>: <see cref="StatisticsMapper"/> shares this exact
    /// catalog-lookup-or-fallback pattern against the same class of raw wire
    /// keys and needs the same sanitization (found in review — it was
    /// missing there).</summary>
    internal static string Sanitize(string raw)
        => raw.Any(char.IsControl)
            ? new string(raw.Where(c => !char.IsControl(c)).ToArray())
            : raw;
}
