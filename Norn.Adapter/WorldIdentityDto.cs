namespace Norn.Adapter;

/// <summary>
/// A world's identity, resolved from a locally-known <c>.fwl</c> file — not
/// derived from any character save. See <see cref="WorldIdentityCatalog"/>.
/// </summary>
/// <param name="SeenAsFiles">
/// Every distinct local <c>.fwl</c> filename stem ever scanned for this
/// <see cref="Uid"/>, in first-seen order — accumulates unconditionally
/// (unlike <see cref="ConflictNote"/>, this is not a disagreement signal,
/// just bookkeeping of which physical files share this identity, e.g. a
/// world copied under a new filename outside the game). Always has at least
/// one entry.
/// </param>
/// <param name="ConflictNote">
/// Set the first time a later scan found a <em>different</em> name/seed for
/// this already-known <see cref="Uid"/> (e.g. a renamed world) — first-seen
/// still wins for <see cref="Name"/>/<see cref="SeedName"/>/<see cref="Seed"/>
/// above, this only surfaces that a disagreement was seen rather than hiding
/// it. Records the first conflict only, not a full history. <c>null</c> when
/// no conflict has ever been observed.
/// </param>
public sealed record WorldIdentityDto(
    long Uid,
    string Name,
    string SeedName,
    int Seed,
    DateTime DateAdded,
    IReadOnlyList<string> SeenAsFiles,
    string? ConflictNote);
