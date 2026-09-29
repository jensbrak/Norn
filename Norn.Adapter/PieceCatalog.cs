using System.Text.Json;

namespace Norn.Adapter;

/// <summary>
/// Norn's sole seam onto the build-piece data (<c>PieceData.json</c>, written
/// by <c>Tools/Vade</c>) — same shape and loading rule as
/// <see cref="RecipeCatalog"/>.
/// </summary>
public static class PieceCatalog
{
    private static IReadOnlyList<PieceDto> _pieces = [];

    public static int Count => _pieces.Count;

    /// <summary>Every piece, in file order.</summary>
    public static IReadOnlyList<PieceDto> All => _pieces;

    public static void Load(params string?[] candidatePaths) =>
        _pieces = JsonCatalogFile.LoadFirst(candidatePaths, Parse) ?? [];

    private static IReadOnlyList<PieceDto> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<PieceFile>(json, JsonCatalogFile.Options)
            ?? throw new JsonException("Piece file is empty.");

        return [.. file.Pieces.Select(p => new PieceDto(
            p.Name,
            p.NameToken,
            p.Tool,
            Enum.IsDefined(typeof(PieceCategory), p.Category) ? (PieceCategory)p.Category : PieceCategory.Misc,
            p.Station,
            p.StationToken,
            p.Season,
            [.. p.Resources.Select(r => new ItemAmount(r.Item, r.Amount))]))];
    }

    // The file's own shape, kept private so the DTOs carry no serialization concerns.
    private sealed record PieceFile(List<PieceEntry> Pieces);

    private sealed record PieceEntry(
        string Name,
        string NameToken,
        string Tool,
        int Category,
        string? Station,
        string? StationToken,
        string? Season,
        List<ResourceEntry> Resources);

    private sealed record ResourceEntry(string Item, int Amount);
}
