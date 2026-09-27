using Norn.Adapter;
using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// The player's row count comes from the <c>invrows</c> unique key that
/// Haldor's extra-row purchases raise — see <see cref="InventoryLayout.HeightOf"/>.
/// </summary>
[Collection(CatalogCollection.Name)]
public class InventoryLayoutTests
{
    // Written by Valheim 1.0.16, rows raised to the game's 9-row cap with the
    // dev console; one item top-left, one bottom-right. Never touched by Norn.
    private const string NineRowFixture = "fixture-v46-nine-rows.fch";

    private static Player PlayerWithUniques(params string[] uniques)
    {
        var player = new Player();
        player.m_uniques.AddRange(uniques);
        return player;
    }

    [Fact]
    public void HeightOf_defaults_to_four_rows_without_the_key()
    {
        Assert.Equal(InventoryLayout.DefaultHeight, InventoryLayout.HeightOf(PlayerWithUniques()));
        Assert.Equal(InventoryLayout.DefaultHeight, InventoryLayout.HeightOf(PlayerWithUniques("invslot1", "")));
    }

    [Theory]
    [InlineData("invrows 4", 4)]
    [InlineData("invrows 5", 5)]
    [InlineData("invrows 6", 6)]
    [InlineData("InvRows 6", 6)]
    [InlineData("invrows 42", 9)]
    [InlineData("invrows -1", 0)]
    public void HeightOf_reads_and_clamps_the_invrows_key(string unique, int expected)
    {
        Assert.Equal(expected, InventoryLayout.HeightOf(PlayerWithUniques("invslot1", unique)));
    }

    [Theory]
    [InlineData("invrows")]
    [InlineData("invrows five")]
    [InlineData("invrowsx 6")]
    public void HeightOf_ignores_malformed_keys(string unique)
    {
        Assert.Equal(InventoryLayout.DefaultHeight, InventoryLayout.HeightOf(PlayerWithUniques(unique)));
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Every_corpus_item_sits_inside_its_characters_grid(string? fileName)
    {
        var path = Corpus.RequireFile(fileName);
        var editor = CharacterEditor.Open(path);
        Assert.SkipWhen(editor is null, $"{fileName} is not openable.");

        var inventory = editor!.View.Inventory;
        Assert.InRange(inventory.Height, InventoryLayout.DefaultHeight, 9);
        Assert.All(inventory.Items, item =>
        {
            Assert.InRange(item.GridX, 0, InventoryLayout.Width - 1);
            Assert.InRange(item.GridY, 0, inventory.Height - 1);
        });
    }

    [Fact]
    public void Nine_row_character_spans_the_full_grid()
    {
        var inventory = CharacterEditor.Open(Path.Combine(TestPaths.FixtureDirectory!, NineRowFixture))!.View.Inventory;

        Assert.Equal(9, inventory.Height);
        Assert.Contains(inventory.Items, i => i.GridX == 0 && i.GridY == 0);
        Assert.Contains(inventory.Items, i => i.GridX == InventoryLayout.Width - 1 && i.GridY == 8);
    }

    [Fact]
    public void Rows_past_the_default_four_accept_items_and_the_last_row_is_the_limit()
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        using var scratch = TempFile.Create();
        File.Copy(Path.Combine(TestPaths.FixtureDirectory!, NineRowFixture), scratch.Path);
        var editor = CharacterEditor.Open(scratch.Path)!;
        var lastRow = editor.View.Inventory.Height - 1;

        editor.AddItemAt(0, lastRow, "Wood");
        Assert.Contains(editor.View.Inventory.Items, i => i.GridX == 0 && i.GridY == lastRow && i.PrefabName == "Wood");

        var countBefore = editor.View.Inventory.Items.Count;
        editor.AddItemAt(0, lastRow + 1, "Wood");
        Assert.Equal(countBefore, editor.View.Inventory.Items.Count);
    }
}
