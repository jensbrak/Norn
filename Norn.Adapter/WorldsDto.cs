namespace Norn.Adapter;

/// <summary>One world's per-character data: spawn/logout/death/home points.
/// <see cref="HasMapData"/> flags presence only — the map blob itself stays
/// opaque, permanently. <see cref="Identity"/> is
/// never derived from the character save itself — it's whatever
/// <see cref="WorldIdentityCatalog"/> happens to know locally about this
/// <see cref="WorldId"/>, <c>null</c> when never locally seen.</summary>
public sealed record WorldDto(
    long WorldId,
    bool HaveCustomSpawnPoint,
    PositionDto SpawnPoint,
    bool HaveLogoutPoint,
    PositionDto LogoutPoint,
    bool HaveDeathPoint,
    PositionDto DeathPoint,
    PositionDto HomePoint,
    bool HasMapData,
    WorldIdentityDto? Identity);

/// <summary>Read-only view for the Worlds tab.</summary>
public sealed record WorldsDto(
    IReadOnlyList<WorldDto> Worlds,
    IReadOnlyList<KeyValuePair<string, float>> KnownWorlds,
    IReadOnlyList<KeyValuePair<string, float>> KnownWorldKeys,
    IReadOnlyList<KeyValuePair<string, float>> KnownCommands);
