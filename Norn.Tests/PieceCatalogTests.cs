using Norn.Adapter;

namespace Norn.Tests;

/// <summary>
/// <see cref="PieceCatalog"/>'s parsing and fallback against synthetic JSON,
/// plus the bundled file's consistency with the bundled item catalog.
/// </summary>
[Collection(CatalogCollection.Name)]
public class PieceCatalogTests
{
    private const string TwoPieces = """
        {
          "pieces": [
            {
              "name": "portal_wood",
              "nameToken": "$piece_portal",
              "tool": "Hammer",
              "category": 0,
              "station": "piece_workbench",
              "stationToken": "$piece_workbench",
              "season": null,
              "resources": [
                { "item": "GreydwarfEye", "amount": 10 },
                { "item": "FineWood", "amount": 20 }
              ]
            },
            {
              "name": "piece_xmastree",
              "nameToken": "$piece_yuletree",
              "tool": "Hammer",
              "category": 99,
              "station": "piece_workbench",
              "stationToken": "$piece_workbench",
              "season": "Yule",
              "resources": [ { "item": "Wood", "amount": 10 } ]
            }
          ]
        }
        """;

    [Fact]
    public void Loads_a_synthetic_file()
    {
        using var temp = TempFile.Create(".json");
        File.WriteAllText(temp.Path, TwoPieces);

        PieceCatalog.Load(temp.Path);

        Assert.Equal(2, PieceCatalog.Count);
        var portal = PieceCatalog.All[0];
        Assert.Equal("$piece_portal", portal.NameToken);
        Assert.Equal("Hammer", portal.Tool);
        Assert.Equal(PieceCategory.Misc, portal.Category);
        Assert.Null(portal.Season);
        Assert.Equal([new ItemAmount("GreydwarfEye", 10), new ItemAmount("FineWood", 20)], portal.Resources);
    }

    [Fact]
    public void An_unknown_category_falls_back_to_misc_and_the_season_is_kept()
    {
        using var temp = TempFile.Create(".json");
        File.WriteAllText(temp.Path, TwoPieces);

        PieceCatalog.Load(temp.Path);

        var tree = PieceCatalog.All[1];
        Assert.Equal(PieceCategory.Misc, tree.Category);
        Assert.Equal("Yule", tree.Season);
    }

    [Fact]
    public void Falls_through_a_corrupt_candidate_to_the_next_and_is_empty_when_none_loads()
    {
        using var corrupt = TempFile.Create(".json");
        using var good = TempFile.Create(".json");
        File.WriteAllText(corrupt.Path, "{ \"pieces\": [ { \"name\": \"portal_wood\" } ] }");
        File.WriteAllText(good.Path, TwoPieces);

        PieceCatalog.Load(corrupt.Path, good.Path);
        Assert.Equal(2, PieceCatalog.Count);

        PieceCatalog.Load(corrupt.Path);
        Assert.Equal(0, PieceCatalog.Count);
    }

    /// <summary>Every piece ingredient must resolve in the bundled item
    /// catalog — Vade checks this at extraction; this guards against the two
    /// files being refreshed out of step.</summary>
    [Fact]
    public void Every_bundled_piece_resource_resolves_in_the_bundled_item_catalog()
    {
        var jsonPath = TestPaths.PieceDataJsonPath;
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(jsonPath is null || csvPath is null, "Bundled Norn.UI/Content files not present.");
        PieceCatalog.Load(jsonPath);
        SharedItemDataCatalog.Load(csvPath);

        Assert.True(PieceCatalog.Count >= 350, $"Only {PieceCatalog.Count} pieces loaded.");
        var missing = PieceCatalog.All
            .SelectMany(p => p.Resources.Select(r => r.ItemName))
            .Where(name => SharedItemDataCatalog.TryFind(name) is null)
            .Distinct()
            .ToList();
        Assert.Empty(missing);
    }
}
