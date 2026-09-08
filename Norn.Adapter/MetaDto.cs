namespace Norn.Adapter;

/// <summary>Read-only view for the Meta tab: identity, save version, file-independent metadata.</summary>
/// <param name="CustomData"><see cref="Player.m_customData"/> — distinct from
/// the per-item <c>ItemData.m_customData</c> already on <see cref="ItemDto"/>.
/// Confirmed against the decompiled source that
/// vanilla Valheim never writes a single key into this dictionary in either
/// decompiled version — a pure mod-extension point, empty for the
/// overwhelming majority of saves, never cleared or pruned by the game once a
/// key exists.</param>
public sealed record MetaDto(
    string PlayerName,
    long PlayerId,
    string StartSeed,
    int ProfileVersion,
    DateTime DateCreated,
    bool UsedCheats,
    bool FirstSpawn,
    int PlayerDataVersion,
    string BeardItem,
    string HairItem,
    ColorDto SkinColor,
    ColorDto HairColor,
    int ModelIndex,
    IReadOnlyList<KeyValuePair<string, string>> CustomData);
