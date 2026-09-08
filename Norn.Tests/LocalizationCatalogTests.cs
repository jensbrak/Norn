using Norn.Adapter;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="LocalizationCatalog"/>'s parsing, <c>$</c>-stripping,
/// and precedence logic against synthetic CSVs, plus spot-checks against the
/// real, tracked <c>Norn.UI/Content/LocalizationData.csv</c> — guarded with
/// <c>Assert.SkipWhen</c> for robustness, same as
/// <see cref="SharedItemDataCatalogTests"/>. Tagged with
/// <see cref="CatalogCollection"/> — see that class's doc comment.
/// </summary>
[Collection(CatalogCollection.Name)]
public class LocalizationCatalogTests
{
    private const string Header = "Name,Text\n";

    [Fact]
    public void Resolves_a_known_key_from_a_synthetic_csv()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header + "piece_forge,Forge\n");

        LocalizationCatalog.Load(temp.Path);

        Assert.Equal("Forge", LocalizationCatalog.TryFind("piece_forge"));
    }

    [Fact]
    public void Strips_a_leading_dollar_sign_the_csv_itself_never_carries()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header + "piece_forge,Forge\n");

        LocalizationCatalog.Load(temp.Path);

        // The wire format's own keys carry a leading "$" (e.g.
        // Player.m_knownStations); the CSV's Name column never does.
        Assert.Equal("Forge", LocalizationCatalog.TryFind("$piece_forge"));
    }

    [Fact]
    public void Handles_a_multiline_quoted_value_with_embedded_commas()
    {
        // The real export's actual shape (tutorial_start_text) — a quoted
        // field spanning several physical lines with an embedded comma. This
        // is exactly the case the old hand-rolled, line-by-line CSV reader
        // could not parse, and the reason LocalizationCatalog uses CsvHelper.
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header
            + "tutorial_start_text,\"Start tips: try to interact with things, like stones.\n\nOpen your inventory with TAB.\"\n");

        LocalizationCatalog.Load(temp.Path);

        var text = LocalizationCatalog.TryFind("tutorial_start_text");
        Assert.NotNull(text);
        Assert.Contains("stones.", text);
        Assert.Contains("Open your inventory with TAB.", text);
        Assert.Contains('\n', text);
    }

    [Fact]
    public void Unresolved_key_returns_null_not_a_default()
    {
        using var temp = TempFile.Create();
        File.WriteAllText(temp.Path, Header + "piece_forge,Forge\n");

        LocalizationCatalog.Load(temp.Path);

        Assert.Null(LocalizationCatalog.TryFind("some_key_not_in_the_catalog"));
    }

    [Fact]
    public void Loads_the_first_existing_candidate_and_ignores_later_ones()
    {
        using var first = TempFile.Create();
        using var second = TempFile.Create();
        File.WriteAllText(first.Path, Header + "from_first,From First\n");
        File.WriteAllText(second.Path, Header + "from_second,From Second\n");
        var missing = TempFile.NonExistentPath(".csv");

        LocalizationCatalog.Load(missing, first.Path, second.Path);

        Assert.NotNull(LocalizationCatalog.TryFind("from_first"));
        Assert.Null(LocalizationCatalog.TryFind("from_second"));
    }

    [Fact]
    public void Missing_and_corrupt_candidates_degrade_to_an_empty_catalog()
    {
        using var corrupt = TempFile.Create();
        // A header that isn't "Name,Text" — GetField("Name") throws when the
        // named column doesn't exist, the deterministic way this fixed,
        // two-column schema actually fails to parse (there's no type
        // coercion here for a bad value to break, unlike SharedItemDataDto's
        // bool/double columns).
        File.WriteAllText(corrupt.Path, "Foo,Bar\nbaz,qux\n");
        var missing = TempFile.NonExistentPath(".csv");

        LocalizationCatalog.Load(missing, corrupt.Path);

        Assert.Equal(0, LocalizationCatalog.Count);
        Assert.Null(LocalizationCatalog.TryFind("anything"));
    }

    [Fact]
    public void Resolves_known_keys_from_the_real_development_csv()
    {
        var path = TestPaths.LocalizationDataCsvPath;
        Assert.SkipWhen(path is null, "Norn.UI/Content/LocalizationData.csv not present.");

        LocalizationCatalog.Load(path);

        Assert.Equal("Forge", LocalizationCatalog.TryFind("piece_forge"));
        Assert.Equal("Forge", LocalizationCatalog.TryFind("$piece_forge"));

        // Confirms the real export's multiline quoted values round-trip
        // correctly through CsvHelper.
        var tutorial = LocalizationCatalog.TryFind("tutorial_start_text");
        Assert.NotNull(tutorial);
        Assert.Contains('\n', tutorial);
    }
}
