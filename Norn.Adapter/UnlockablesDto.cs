namespace Norn.Adapter;

/// <summary>One known crafting station and its upgrade level. <see cref="Name"/>
/// is the resolved display name where <see cref="LocalizationCatalog"/> covers
/// it, the raw wire key otherwise — not necessarily
/// unique the way the raw key was; see <see cref="UnlockablesDto"/>'s remarks.</summary>
public sealed record KnownStationDto(string Name, int Level);

/// <summary>One read text: a short label and its full body, both resolved
/// from the two localization keys the wire actually stores for each entry
/// (an earlier pass assumed the wire held literal text
/// directly; it doesn't) via <see cref="LocalizationCatalog"/>, falling back
/// to the raw key on a miss. See <see cref="UnlockablesDto.KnownTexts"/>.</summary>
public sealed record KnownTextDto(string Label, string Text);

/// <summary>Read-only view for the Unlockables tab. Every field's source list
/// is a de-duplicated set on the wire (<c>OrderedCollections.AddToSet</c>/
/// <c>.Add</c>/<c>.Set</c> — see <see cref="Norn.GameCore.Player"/>), so each
/// *raw wire* entry has a stable, unique key (the string itself, or a pair's
/// first element).
/// <para>
/// <see cref="KnownRecipes"/>, <see cref="KnownStations"/>,
/// <see cref="KnownMaterial"/>, <see cref="Trophies"/>, and
/// <see cref="KnownTexts"/> carry the <em>resolved</em> display string in
/// place of that raw key (<see cref="UnlockablesMapper"/>)
/// — a deliberate simplification for this read-only display pass, but it
/// means the "stable, unique key" guarantee above no longer holds for those
/// five fields as displayed: two distinct raw keys resolving to the same
/// English text (unlikely, not impossible) would look identical here. A
/// future delete-by-value feature (deferred) keying off what's
/// shown on screen would need to resolve back to the raw wire value first,
/// not assume the displayed string is what the raw key uniqueness guarantee
/// was ever about — flagged here so that's a deliberate check then, not a
/// rediscovered surprise. <see cref="Uniques"/>, <see cref="KnownBiomes"/>,
/// and <see cref="ShownTutorials"/> are unaffected — still raw, still
/// keyed exactly as the comment above describes.
/// </para>
/// <see cref="ShownTutorials"/> is declared last deliberately: same wire
/// shape as the other seven, but a UI-nag-suppression flag rather than a
/// real discovery, so it is least prominent regardless of where
/// <c>Player.Load</c> reads it.</summary>
public sealed record UnlockablesDto(
    IReadOnlyList<string> KnownRecipes,
    IReadOnlyList<KnownStationDto> KnownStations,
    IReadOnlyList<string> KnownMaterial,
    IReadOnlyList<string> Uniques,
    IReadOnlyList<string> Trophies,
    IReadOnlyList<string> KnownBiomes,
    IReadOnlyList<KnownTextDto> KnownTexts,
    IReadOnlyList<string> ShownTutorials);
