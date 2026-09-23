using Norn.Adapter;
using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Covers <see cref="CharacterEditor.MoveItemAt"/> — the inventory tab's
/// click-to-carry move/merge/swap/split primitive. Tagged with
/// <see cref="CatalogCollection"/>, same reasoning as
/// <see cref="CharacterEditorCatalogTests"/>'s own doc comment: every test
/// here resolves catalog data. Scenarios are manufactured via
/// <see cref="CharacterEditor.AddItemAt"/>/<see cref="CharacterEditor.AddItemsAt"/>
/// against real catalog prefabs rather than hunting for a naturally
/// occurring corpus item in the right state — the same shape
/// <see cref="CharacterEditorCatalogTests.AddItemsAt_guarantees_the_anchor_and_spills_overflow_into_the_partial_stack"/>
/// already uses, and the only practical way to get a controlled two-item
/// scenario at all.
/// <para>
/// Not covered: the real game's <c>m_worldLevel</c>-mismatch dead-drop
/// (<see cref="CharacterEditor.MoveItemAt"/>'s own doc comment) — nothing in
/// Norn's public API sets <c>m_worldLevel</c> on an item, so this scenario
/// can't be manufactured without reaching into <c>GameCore</c> internals a
/// test shouldn't need. The branch itself is a direct, simple field
/// comparison; flagged here as a real, deliberate gap rather than silently
/// skipped.
/// </para>
/// </summary>
[Collection(CatalogCollection.Name)]
public class CharacterEditorDragDropTests
{
    /// <summary>Opens a scratch copy of a corpus file, skipping (via
    /// <see cref="Assert.SkipWhen"/>, which throws — never returns
    /// normally on a skip) through every precondition the rest of this
    /// suite's tests already check individually. Non-nullable: by the time
    /// this returns, every check has passed.</summary>
    private static (CharacterEditor Editor, TempFile Scratch) OpenScratch(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        return (editor!, scratch);
    }

    private static List<(int X, int Y)> EmptySlots(CharacterEditor editor)
    {
        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        return Enumerable.Range(0, InventoryLayout.Height)
            .SelectMany(y => Enumerable.Range(0, InventoryLayout.Width).Select(x => (x, y)))
            .Where(pos => !occupied.Contains(pos))
            .ToList();
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Full_move_to_empty_slot_repositions_and_round_trips(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");

        var prefabName = SharedItemDataCatalog.All.First().ItemName;
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        editor.AddItemAt(ax, ay, prefabName);
        var before = editor.View.Inventory.Items.Single(i => i.GridX == ax && i.GridY == ay);

        var placed = editor.MoveItemAt(ax, ay, before.Stack, bx, by);
        Assert.Equal(before.Stack, placed);
        Assert.True(editor.IsDirty);

        Assert.DoesNotContain(editor.View.Inventory.Items, i => i.GridX == ax && i.GridY == ay);
        var moved = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(prefabName, moved.PrefabName);
        Assert.Equal(before.Stack, moved.Stack);
        Assert.Equal(before.Quality, moved.Quality);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(prefabName, reloaded.PrefabName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Same_slot_placement_is_a_no_op(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 1, $"{fileName} has no free slot.");

        var prefabName = SharedItemDataCatalog.All.First().ItemName;
        var (x, y) = slots[0];
        editor.AddItemAt(x, y, prefabName);
        var dirtyAfterAdd = editor.IsDirty;

        var placed = editor.MoveItemAt(x, y, 1, x, y);
        Assert.Equal(0, placed);
        Assert.Equal(dirtyAfterAdd, editor.IsDirty);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Split_to_empty_slot_creates_a_new_stack_and_shrinks_the_source(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var shared = SharedItemDataCatalog.All.FirstOrDefault(s => s.MaxStack >= 4);
        Assert.SkipWhen(shared is null, "Catalog has no item with MaxStack >= 4.");

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        editor.AddItemsAt(ax, ay, shared!.ItemName, 4, setCrafter: false);
        var placed = editor.MoveItemAt(ax, ay, 1, bx, by);
        Assert.Equal(1, placed);

        var source = editor.View.Inventory.Items.Single(i => i.GridX == ax && i.GridY == ay);
        var split = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(3, source.Stack);
        Assert.Equal(1, split.Stack);
        Assert.Equal(shared.ItemName, split.PrefabName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Placing_onto_a_different_item_swaps_positions_only(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var catalog = SharedItemDataCatalog.All.Select(s => s.ItemName).Distinct().Take(2).ToList();
        Assert.SkipWhen(catalog.Count < 2, "Catalog has fewer than two distinct entries.");

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        editor.AddItemAt(ax, ay, catalog[0]);
        editor.AddItemAt(bx, by, catalog[1]);

        var placed = editor.MoveItemAt(ax, ay, 1, bx, by);
        Assert.Equal(1, placed);

        var atA = editor.View.Inventory.Items.Single(i => i.GridX == ax && i.GridY == ay);
        var atB = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(catalog[1], atA.PrefabName);
        Assert.Equal(catalog[0], atB.PrefabName);
    }

    /// <summary>
    /// Regression test: every other
    /// scenario in this file manufactures its items via
    /// <see cref="CharacterEditor.AddItemAt"/>, which sets a real, non-empty
    /// <c>PrefabName</c> on the underlying <c>Inventory.ItemData</c> — but a
    /// save file's own items (item version 108+, i.e. any current save) carry
    /// only <c>PrefabHash</c>; the raw <c>PrefabName</c> field is empty.
    /// <see cref="CharacterEditor.MoveItemAt"/> originally compared on
    /// <c>PrefabName</c>, which made every real pair of items look
    /// identical to it (both <c>""</c>) regardless of what they actually
    /// were — a drop of one item type onto a different, stackable type
    /// silently forced a merge instead of a swap, destroying the dropped
    /// item. This test deliberately uses two of the corpus file's own,
    /// already-loaded items rather than manufactured ones, which is the only
    /// way to reproduce the empty-<c>PrefabName</c> condition that exposed
    /// the bug.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Swap_between_two_real_preexisting_corpus_items_does_not_merge_them(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var items = editor.View.Inventory.Items;
        var pair = items
            .SelectMany(_ => items, (a, b) => (A: a, B: b))
            .FirstOrDefault(p => p.A.PrefabName != p.B.PrefabName);
        Assert.SkipWhen(pair.A is null, $"{fileName} doesn't have two distinct, resolvable items to test with.");

        var (aBefore, bBefore) = pair;
        var (ax, ay) = (aBefore.GridX, aBefore.GridY);
        var (bx, by) = (bBefore.GridX, bBefore.GridY);

        var placed = editor.MoveItemAt(ax, ay, aBefore.Stack, bx, by);
        Assert.True(placed > 0);

        // A swap: both items still fully exist afterward, just exchanged
        // positions. The bug's symptom was the opposite — one item gone
        // entirely, the other's stack grown by a forced, invalid merge.
        var atB = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        var atA = editor.View.Inventory.Items.SingleOrDefault(i => i.GridX == ax && i.GridY == ay);
        Assert.NotNull(atA);
        Assert.Equal(aBefore.PrefabName, atB.PrefabName);
        Assert.Equal(bBefore.PrefabName, atA!.PrefabName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Merging_matching_stacks_combines_within_capacity_and_removes_the_source(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var shared = SharedItemDataCatalog.All.FirstOrDefault(s => s.MaxStack >= 6);
        Assert.SkipWhen(shared is null, "Catalog has no item with MaxStack >= 6.");

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        editor.AddItemsAt(ax, ay, shared!.ItemName, 2, setCrafter: false);
        editor.AddItemsAt(bx, by, shared.ItemName, 3, setCrafter: false);

        var placed = editor.MoveItemAt(ax, ay, 2, bx, by);
        Assert.Equal(2, placed);

        Assert.DoesNotContain(editor.View.Inventory.Items, i => i.GridX == ax && i.GridY == ay);
        var merged = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(5, merged.Stack);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(5, reloaded.Stack);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Merge_overflow_caps_the_target_and_returns_the_partial_amount_placed(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var shared = SharedItemDataCatalog.All.FirstOrDefault(s => s.MaxStack is >= 4 and < 999);
        Assert.SkipWhen(shared is null, "Catalog has no item with a small-enough MaxStack to overflow deliberately.");

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        var max = shared!.MaxStack;
        var targetStack = max - 1;
        var sourceStack = max; // guarantees an overflow: (max - 1) + max > max

        editor.AddItemsAt(bx, by, shared.ItemName, targetStack, setCrafter: false);
        editor.AddItemsAt(ax, ay, shared.ItemName, sourceStack, setCrafter: false);

        var placed = editor.MoveItemAt(ax, ay, sourceStack, bx, by);
        Assert.Equal(1, placed); // exactly the space that was left: max - (max - 1)

        var target = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(max, target.Stack);

        var remainder = editor.View.Inventory.Items.Single(i => i.GridX == ax && i.GridY == ay);
        Assert.Equal(sourceStack - placed, remainder.Stack);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Merge_leaves_the_targets_crafter_tag_unchanged(string? fileName)
    {
        var (editor, scratch) = OpenScratch(fileName);
        using var _ = scratch;

        var shared = SharedItemDataCatalog.All.FirstOrDefault(s => s.MaxStack >= 6 && s.CanHaveCrafterTag);
        Assert.SkipWhen(shared is null, "Catalog has no stackable, crafter-taggable item.");

        var slots = EmptySlots(editor);
        Assert.SkipWhen(slots.Count < 2, $"{fileName} doesn't have two free slots.");
        var (ax, ay) = slots[0];
        var (bx, by) = slots[1];

        editor.AddItemsAt(ax, ay, shared!.ItemName, 2, setCrafter: true); // source carries a crafter tag
        editor.AddItemsAt(bx, by, shared.ItemName, 3, setCrafter: false); // target has none

        editor.MoveItemAt(ax, ay, 2, bx, by);

        var merged = editor.View.Inventory.Items.Single(i => i.GridX == bx && i.GridY == by);
        Assert.Equal(0, merged.CrafterId); // target's own (none) stands — the source's tag was discarded, not copied
    }
}
