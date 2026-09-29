namespace Norn.Adapter;

/// <summary>
/// One buildable piece as the game registers it (a <c>Piece</c> in a tool's
/// piece table), from <see cref="PieceCatalog"/>. Game facts only.
/// </summary>
/// <param name="Name">The piece's prefab name (<c>portal_wood</c>).</param>
/// <param name="NameToken">Its localization token (<c>$piece_portal</c>) —
/// pieces aren't items, so they aren't named through
/// <see cref="SharedItemDataCatalog"/>.</param>
/// <param name="Tool">The tool that builds it: <c>Hammer</c>, <c>Hoe</c> or
/// <c>Cultivator</c>.</param>
/// <param name="Station">The station that must be nearby, by prefab name, or
/// <c>null</c> for none.</param>
/// <param name="Season">The season that switches the piece on (<c>Yule</c>), or
/// <c>null</c> for one that's always available.</param>
public sealed record PieceDto(
    string Name,
    string NameToken,
    string Tool,
    PieceCategory Category,
    string? Station,
    string? StationToken,
    string? Season,
    IReadOnlyList<ItemAmount> Resources);
