namespace Norn.Adapter;

/// <summary>
/// A world's decoded exploration data, for display only — see
/// <see cref="CharacterEditor.DecodeWorldMap"/>. The underlying blob stays
/// opaque on every write path; this is decoded to be shown, never re-encoded.
/// <see cref="OwnPinCount"/>/<see cref="ReceivedPinCount"/> aside, pins
/// themselves are decoded by <c>Norn.GameCore.Minimap.Decode</c> but
/// deliberately not carried any further — a character editor showing live,
/// addable/removable gameplay markers implies an edit surface Norn isn't
/// taking on, and a well-played world's pin count can reach into the
/// thousands, which would need its own visibility toggle just to stay
/// legible.
/// </summary>
/// <param name="OwnPinCount">Pins with <c>m_ownerID == 0</c> — the exact
/// partition the game itself uses everywhere (visibility, tinting,
/// <c>resetsharedmap</c>; confirmed against the decompiled source). Named
/// "own", not "authored by me": clicking
/// a received pin once adopts it (resets its owner to 0), so this really
/// means "not currently attributed to anyone else" — the game's own
/// definition, not a Norn approximation of it.</param>
/// <param name="ReceivedPinCount">Pins with a non-zero <c>m_ownerID</c> —
/// received via a cartography table and not yet adopted.</param>
public sealed record WorldMapDto(
    int TextureSize,
    byte[] Explored,
    byte[] ExploredOthers,
    int OwnPinCount,
    int ReceivedPinCount);
