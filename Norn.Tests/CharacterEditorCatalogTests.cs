using Norn.Adapter;
using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Split out of <see cref="CharacterEditorTests"/> (found in review): the
/// only <see cref="CharacterEditor"/> tests that touch
/// <see cref="SharedItemDataCatalog"/> — six via <see cref="SharedItemDataCatalog.Load"/>,
/// one (<see cref="Set_item_crafter_round_trips_through_save_and_reload"/>)
/// only via <see cref="SharedItemDataCatalog.TryFind"/>, with no <c>Load</c>
/// call of its own — reading the catalog needs the same serialization
/// against a concurrent <c>Load</c> as writing it does. Tagged with
/// <see cref="CatalogCollection"/>, same shared static as
/// <see cref="SharedItemDataCatalogTests"/>/<see cref="LocalizationCatalogTests"/>/
/// <see cref="WorldIdentityCatalogTests"/>/<see cref="AdapterMappingTests"/> —
/// see that collection's own doc comment. The remaining ~30
/// <see cref="CharacterEditor"/> tests never reference any shared catalog at
/// all (verified by grep across the whole file, not assumed), so they stay
/// in <see cref="CharacterEditorTests"/> without this tag, free to run in
/// parallel with everything here.
/// </summary>
[Collection(CatalogCollection.Name)]
public class CharacterEditorCatalogTests
{
    /// <summary>
    /// Covers repair, quality-up (with its auto-repair coupling), fill-stack,
    /// and delete together in one pass — same batching-by-
    /// combined-invariant shape as <see cref="CharacterEditorTests.Restore_vitals_round_trips_through_save_and_reload"/>,
    /// rather than one test per mutator. Needs a corpus item that resolves
    /// against the real catalog and both uses durability and has quality
    /// levels; skips (not fails) when no such item exists in this corpus
    /// file, or the catalog itself is absent — the corpus's actual contents
    /// aren't something this suite controls.
    /// </summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Inventory_repair_and_quality_up_round_trip_through_save_and_reload(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { UsesDurability: true, MaxQuality: > 1 } shared
            && i.Quality < shared.MaxQuality);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max durability+quality item resolvable against the catalog.");

        var targetShared = SharedItemDataCatalog.TryFind(target!.PrefabName)!;

        // Repair asserted on its own, BEFORE the quality change (found in
        // review): a quality upgrade re-repairs to the new ceiling anyway,
        // so checking durability only after both operations would pass even
        // if RepairItemAt did nothing at all. Only meaningful when the item
        // is actually damaged to begin with — repairing a pristine item is
        // a legitimate no-op, and must not dirty the session.
        var damaged = target.Durability < (float)targetShared.MaxDurabilityFor(target.Quality);
        editor.RepairItemAt(target.GridX, target.GridY);
        Assert.Equal(damaged, editor.IsDirty);

        if (damaged)
        {
            var repaired = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
            Assert.Equal((float)targetShared.MaxDurabilityFor(repaired.Quality), repaired.Durability);
        }

        editor.SetItemQuality(target.GridX, target.GridY, target.Quality + 1);
        Assert.True(editor.IsDirty);

        var updated = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        var shared = SharedItemDataCatalog.TryFind(updated.PrefabName)!;
        Assert.Equal(target.Quality + 1, updated.Quality);
        Assert.Equal((float)shared.MaxDurabilityFor(updated.Quality), updated.Durability);

        editor.Save();
        Assert.False(editor.IsDirty);

        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(target.Quality + 1, reloaded.Quality);
        Assert.Equal((float)shared.MaxDurabilityFor(reloaded.Quality), reloaded.Durability);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Fill_item_stack_round_trips_through_save_and_reload(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 } shared && i.Stack < shared.MaxStack);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max stackable item resolvable against the catalog.");

        var maxStack = SharedItemDataCatalog.TryFind(target!.PrefabName)!.MaxStack;
        editor.FillItemStack(target.GridX, target.GridY);
        Assert.True(editor.IsDirty);
        Assert.Equal(maxStack, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(maxStack, reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_item_stack_round_trips_through_save_and_reload(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 });
        Assert.SkipWhen(target is null, $"{fileName} has no stackable item resolvable against the catalog.");

        var maxStack = SharedItemDataCatalog.TryFind(target!.PrefabName)!.MaxStack;
        // Guaranteed different from the item's current stack either way
        // (MaxStack > 1 means maxStack - 1 >= 1), so IsDirty is meaningfully
        // asserted below rather than trivially true from a no-op match.
        var requested = target.Stack < maxStack ? target.Stack + 1 : maxStack - 1;

        editor.SetItemStack(target.GridX, target.GridY, requested);
        Assert.True(editor.IsDirty);
        Assert.Equal(requested, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        Assert.Equal(requested, reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);
    }

    /// <summary>Confirms the clamp at both ends, including the deliberate
    /// floor of 1 — a departure from a previous, since-removed version of
    /// <c>SetItemStack</c>, which floored at 0.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_item_stack_clamps_to_catalog_bounds(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 });
        Assert.SkipWhen(target is null, $"{fileName} has no stackable item resolvable against the catalog.");

        var maxStack = SharedItemDataCatalog.TryFind(target!.PrefabName)!.MaxStack;

        editor.SetItemStack(target.GridX, target.GridY, maxStack + 1000);
        Assert.Equal(maxStack, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);

        editor.SetItemStack(target.GridX, target.GridY, 0);
        Assert.Equal(1, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);

        editor.SetItemStack(target.GridX, target.GridY, -50);
        Assert.Equal(1, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);
    }

    /// <summary>Mirrors <see cref="Add_item_with_fill_stack_requested_does_not_stack_a_non_stackable_item"/>'s
    /// guard, directly against <see cref="CharacterEditor.SetItemStack"/>
    /// this time rather than through <c>AddItemAt</c> + fill.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_item_stack_does_not_stack_a_non_stackable_item(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: <= 1 });
        Assert.SkipWhen(target is null, $"{fileName} has no non-stackable item resolvable against the catalog.");

        editor.SetItemStack(target!.GridX, target.GridY, 5);
        Assert.False(editor.IsDirty);
        Assert.Equal(target.Stack, editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY).Stack);
    }

    /// <summary>Picks any prefab name already present in this file's own
    /// inventory as a known-resolvable one, rather than assuming a specific
    /// item exists across every corpus file. Skips if none resolve, or the
    /// grid is already full (32/32).</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Add_item_round_trips_through_save_and_reload(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var prefabName = editor!.View.Inventory.Items
            .Select(i => i.PrefabName)
            .FirstOrDefault(name => SharedItemDataCatalog.TryFind(name) is not null);
        Assert.SkipWhen(prefabName is null, $"{fileName} has no item resolvable against the catalog.");

        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        (int X, int Y)? emptySlot = null;
        for (var y = 0; y < InventoryLayout.Height && emptySlot is null; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    emptySlot = (x, y);
                    break;
                }
            }
        }

        Assert.SkipWhen(emptySlot is null, $"{fileName}'s inventory is already full.");

        var shared = SharedItemDataCatalog.TryFind(prefabName!)!;
        editor.AddItemAt(emptySlot!.Value.X, emptySlot.Value.Y, prefabName!);
        Assert.True(editor.IsDirty);

        var added = editor.View.Inventory.Items.Single(i => i.GridX == emptySlot.Value.X && i.GridY == emptySlot.Value.Y);
        Assert.Equal(prefabName, added.PrefabName);
        Assert.Equal(1, added.Stack);
        Assert.Equal(1, added.Quality);
        Assert.Equal(0, added.CrafterId);
        Assert.Equal("", added.CrafterName);
        if (shared.UsesDurability)
        {
            Assert.Equal((float)shared.MaxDurabilityFor(1), added.Durability);
        }

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == emptySlot.Value.X && i.GridY == emptySlot.Value.Y);
        Assert.Equal(prefabName, reloaded.PrefabName);
        Assert.Equal(1, reloaded.Stack);
        Assert.Equal(1, reloaded.Quality);
    }

    /// <summary>Closes the loop on a concern about the Add Item
    /// window's "Fill stack (if possible)" checkbox: it's a blanket toggle,
    /// unconditioned on which item is selected, so the real safety has to be
    /// <see cref="CharacterEditor.FillItemStack"/>'s own existing
    /// <c>MaxStack &lt;= 1</c> guard applying at add time — this asserts that
    /// guard holds even for a freshly-added, non-stackable item, not just
    /// for the already-in-inventory items the original Fill-stack tests
    /// cover. Needs a resolvable non-stackable item already present in the
    /// file (to know a real prefab name); skips if none exists or the grid
    /// is full.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Add_item_with_fill_stack_requested_does_not_stack_a_non_stackable_item(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var prefabName = editor!.View.Inventory.Items
            .Select(i => i.PrefabName)
            .FirstOrDefault(name => SharedItemDataCatalog.TryFind(name) is { MaxStack: <= 1 });
        Assert.SkipWhen(prefabName is null, $"{fileName} has no non-stackable item resolvable against the catalog.");

        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        (int X, int Y)? emptySlot = null;
        for (var y = 0; y < InventoryLayout.Height && emptySlot is null; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    emptySlot = (x, y);
                    break;
                }
            }
        }

        Assert.SkipWhen(emptySlot is null, $"{fileName}'s inventory is already full.");

        editor.AddItemAt(emptySlot!.Value.X, emptySlot.Value.Y, prefabName!);
        editor.FillItemStack(emptySlot.Value.X, emptySlot.Value.Y);

        var added = editor.View.Inventory.Items.Single(i => i.GridX == emptySlot.Value.X && i.GridY == emptySlot.Value.Y);
        Assert.Equal(1, added.Stack);
    }

    /// <summary>Gated on <see cref="SharedItemDataCatalog"/>, superseding
    /// this doc comment's own earlier
    /// claim, once the catalog's own recipe-output extraction made the
    /// per-item fact available. Needs a corpus item where the resolved catalog entry
    /// doesn't positively say <c>CanHaveCrafterTag: false</c> — an
    /// unresolved item is eligible too (unknown isn't unsupported), same
    /// permissive-when-unresolved shape as the mutator's own gate.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Set_item_crafter_round_trips_through_save_and_reload(string? fileName)
    {
        // Loads the catalog explicitly rather than relying on whichever
        // sibling test in this collection happened to run first — the
        // eligibility gate below is a catalog lookup, so an unloaded
        // catalog silently changes which items this test considers.
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        // Excludes items already stamped with this profile's own id (found
        // in review): the mutator correctly no-ops on those, so picking one
        // made every assertion below trivially true — the crafter fields
        // already held the expected values before the call.
        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            i.CrafterId != editor.View.Meta.PlayerId
            && (SharedItemDataCatalog.TryFind(i.PrefabName)?.CanHaveCrafterTag ?? true));
        Assert.SkipWhen(target is null, $"{fileName} has no item eligible for a crafter tag.");

        editor.SetItemCrafterAt(target!.GridX, target.GridY);
        Assert.True(editor.IsDirty);

        var updated = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(editor.View.Meta.PlayerId, updated.CrafterId);
        Assert.Equal(editor.View.Meta.PlayerName, updated.CrafterName);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(reopened.View.Meta.PlayerId, reloaded.CrafterId);
        Assert.Equal(reopened.View.Meta.PlayerName, reloaded.CrafterName);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Repair_item_edit_does_not_remap_the_unrelated_DTOs(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        // Must be a *damaged* item (found in review): repairing one already
        // at full durability is a legitimate no-op that correctly remaps
        // nothing, leaving the NotSame assertion below with nothing to
        // observe.
        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { UsesDurability: true } shared
            && i.Durability < (float)shared.MaxDurabilityFor(i.Quality));
        Assert.SkipWhen(target is null, $"{fileName} has no damaged durability item resolvable against the catalog.");

        var before = editor.View;
        editor.RepairItemAt(target!.GridX, target.GridY);

        Assert.Same(before.Meta, editor.View.Meta);
        Assert.Same(before.Skills, editor.View.Skills);
        Assert.Same(before.Vitals, editor.View.Vitals);
        Assert.Same(before.Statistics, editor.View.Statistics);
        Assert.Same(before.Worlds, editor.View.Worlds);
        Assert.NotSame(before.Inventory, editor.View.Inventory);
    }

    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void Repair_all_repairs_every_resolvable_durability_item(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);
        var hasDurabilityItem = editor!.View.Inventory.Items.Any(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { UsesDurability: true });
        Assert.SkipWhen(!hasDurabilityItem, $"{fileName} has no durability item resolvable against the catalog.");

        // Dirty exactly when something actually needed repairing — a file
        // whose durability items are all already full is a genuine no-op
        // (found in review). The post-condition below still holds either
        // way, and is the real subject of this test.
        var anyDamaged = editor.View.Inventory.Items.Any(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { UsesDurability: true } shared
            && i.Durability < (float)shared.MaxDurabilityFor(i.Quality));

        editor.RepairAllItems();
        Assert.Equal(anyDamaged, editor.IsDirty);

        foreach (var item in editor.View.Inventory.Items)
        {
            if (SharedItemDataCatalog.TryFind(item.PrefabName) is { UsesDurability: true } shared)
            {
                Assert.Equal((float)shared.MaxDurabilityFor(item.Quality), item.Durability);
            }
        }
    }

    /// <summary>Proves the merge-over-new-slot preference: with both a
    /// below-max stack and an empty slot available, an amount that fits
    /// entirely in the existing stack must land there, not in a new one.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void AddItems_tops_up_an_existing_partial_stack_before_creating_a_new_one(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 } shared && i.Stack < shared.MaxStack);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max stackable item resolvable against the catalog.");

        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        var hasEmptySlot = Enumerable.Range(0, InventoryLayout.Height)
            .SelectMany(y => Enumerable.Range(0, InventoryLayout.Width).Select(x => (x, y)))
            .Any(pos => !occupied.Contains(pos));
        Assert.SkipWhen(!hasEmptySlot, $"{fileName}'s inventory is already full.");

        var shared = SharedItemDataCatalog.TryFind(target!.PrefabName)!;
        var room = shared.MaxStack - target.Stack;
        var countBefore = editor.View.Inventory.Items.Count;

        editor.AddItems(target.PrefabName, room, setCrafter: false);
        Assert.True(editor.IsDirty);

        // Same item count as before an empty slot was available too — the
        // whole request fit in the existing stack, so no new one was made.
        Assert.Equal(countBefore, editor.View.Inventory.Items.Count);
        var updated = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(shared.MaxStack, updated.Stack);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloaded = reopened!.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(shared.MaxStack, reloaded.Stack);
    }

    /// <summary>Proves split-to-fit: an amount bigger than one stack tops
    /// the existing partial stack up to max, then spills the remainder into
    /// exactly one brand-new stack.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void AddItems_splits_a_large_amount_across_the_partial_stack_and_a_new_slot(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 } shared && i.Stack < shared.MaxStack);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max stackable item resolvable against the catalog.");

        var occupiedCount = editor.View.Inventory.Items.Count;
        Assert.SkipWhen(occupiedCount >= InventoryLayout.Width * InventoryLayout.Height,
            $"{fileName}'s inventory has no room for a new stack.");

        var shared = SharedItemDataCatalog.TryFind(target!.PrefabName)!;
        var beforePositions = editor.View.Inventory.Items
            .Where(i => i.PrefabName == target.PrefabName)
            .Select(i => (i.GridX, i.GridY))
            .ToHashSet();
        var beforeTotal = editor.View.Inventory.Items.Where(i => i.PrefabName == target.PrefabName).Sum(i => i.Stack);

        var room = shared.MaxStack - target.Stack;
        var amount = room + shared.MaxStack; // fully tops up the partial stack, then fills one whole new stack

        editor.AddItems(target.PrefabName, amount, setCrafter: false);
        Assert.True(editor.IsDirty);

        var afterItems = editor.View.Inventory.Items.Where(i => i.PrefabName == target.PrefabName).ToList();
        Assert.Equal(beforePositions.Count + 1, afterItems.Count);
        Assert.Equal(beforeTotal + amount, afterItems.Sum(i => i.Stack));

        var updatedTarget = afterItems.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(shared.MaxStack, updatedTarget.Stack);

        var newStack = afterItems.Single(i => !beforePositions.Contains((i.GridX, i.GridY)));
        Assert.Equal(shared.MaxStack, newStack.Stack);
    }

    /// <summary>Proves anchor authority: a specific empty tile (the target of
    /// an empty tile's own "Add item" menu entry) is always the one that gets
    /// filled first, up to its own MaxStack — only the overflow beyond that
    /// spills into topping up an unrelated partial stack elsewhere.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void AddItemsAt_guarantees_the_anchor_and_spills_overflow_into_the_partial_stack(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 } shared && i.Stack < shared.MaxStack);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max stackable item resolvable against the catalog.");

        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        (int X, int Y)? anchor = null;
        for (var y = 0; y < InventoryLayout.Height && anchor is null; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    anchor = (x, y);
                    break;
                }
            }
        }

        Assert.SkipWhen(anchor is null, $"{fileName}'s inventory is already full.");

        var shared = SharedItemDataCatalog.TryFind(target!.PrefabName)!;
        var room = shared.MaxStack - target.Stack;
        var amount = shared.MaxStack + room; // fills the anchor to max, spills exactly `room` into the partial stack

        editor.AddItemsAt(anchor!.Value.X, anchor.Value.Y, target.PrefabName, amount, setCrafter: false);
        Assert.True(editor.IsDirty);

        var atAnchor = editor.View.Inventory.Items.Single(i => i.GridX == anchor.Value.X && i.GridY == anchor.Value.Y);
        Assert.Equal(target.PrefabName, atAnchor.PrefabName);
        Assert.Equal(shared.MaxStack, atAnchor.Stack);

        var updatedTarget = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(shared.MaxStack, updatedTarget.Stack);

        editor.Save();
        var reopened = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(reopened);
        var reloadedAnchor = reopened!.View.Inventory.Items.Single(i => i.GridX == anchor.Value.X && i.GridY == anchor.Value.Y);
        Assert.Equal(shared.MaxStack, reloadedAnchor.Stack);
    }

    /// <summary>No-op, cleanly, when the grid is completely full and every
    /// existing stack of the requested item is already maxed — there is
    /// nowhere left to put anything.</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void AddItems_no_ops_when_grid_is_full_and_nothing_fits(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var prefabName = editor!.View.Inventory.Items
            .Select(i => i.PrefabName)
            .FirstOrDefault(name => SharedItemDataCatalog.TryFind(name) is { MaxStack: > 1 });
        Assert.SkipWhen(prefabName is null, $"{fileName} has no stackable item resolvable against the catalog.");

        // Max out every stack (nothing left to merge into), then fill every
        // remaining empty slot with the same resolvable prefab (nowhere
        // left to place a new stack either).
        editor.FillAllStacks();

        var occupied = editor.View.Inventory.Items.Select(i => (i.GridX, i.GridY)).ToHashSet();
        for (var y = 0; y < InventoryLayout.Height; y++)
        {
            for (var x = 0; x < InventoryLayout.Width; x++)
            {
                if (!occupied.Contains((x, y)))
                {
                    editor.AddItemAt(x, y, prefabName!);
                }
            }
        }

        editor.FillAllStacks();

        var totalBefore = editor.View.Inventory.Items.Where(i => i.PrefabName == prefabName).Sum(i => i.Stack);
        var countBefore = editor.View.Inventory.Items.Count;

        editor.AddItems(prefabName!, 1, setCrafter: false);

        Assert.Equal(totalBefore, editor.View.Inventory.Items.Where(i => i.PrefabName == prefabName).Sum(i => i.Stack));
        Assert.Equal(countBefore, editor.View.Inventory.Items.Count);
    }

    /// <summary>Proves the category filter actually excludes non-matching
    /// items, not just that filling itself works (already covered by
    /// <see cref="Fill_item_stack_round_trips_through_save_and_reload"/>).</summary>
    [Theory]
    [MemberData(nameof(Corpus.Files), MemberType = typeof(Corpus))]
    public void FillStacksOfType_only_fills_items_in_the_given_categories(string? fileName)
    {
        var csvPath = TestPaths.SharedItemDataCsvPath;
        Assert.SkipWhen(csvPath is null, "Norn.UI/Content/SharedItemData.csv not present.");
        SharedItemDataCatalog.Load(csvPath);

        var corpusPath = Corpus.RequireFile(fileName);
        var probe = new PlayerProfile(corpusPath);
        Assert.SkipWhen(!probe.Load(), $"{fileName} is outside the compatible profile-version range.");
        Assert.SkipWhen(PlayerLoader.Load(probe) is null, $"{fileName} has no inner player-data blob.");

        using var scratch = TempFile.Create();
        File.Copy(corpusPath, scratch.Path);

        var editor = CharacterEditor.Open(scratch.Path);
        Assert.NotNull(editor);

        var target = editor!.View.Inventory.Items.FirstOrDefault(i =>
            SharedItemDataCatalog.TryFind(i.PrefabName) is { MaxStack: > 1 } shared && i.Stack < shared.MaxStack);
        Assert.SkipWhen(target is null, $"{fileName} has no below-max stackable item resolvable against the catalog.");

        var shared = SharedItemDataCatalog.TryFind(target!.PrefabName)!;
        var otherType = Enum.GetValues<ItemType>().First(t => t != shared.ItemType);

        editor.FillStacksOfType(new HashSet<ItemType> { otherType });
        var untouched = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(target.Stack, untouched.Stack);

        editor.FillStacksOfType(new HashSet<ItemType> { shared.ItemType });
        Assert.True(editor.IsDirty);
        var filled = editor.View.Inventory.Items.Single(i => i.GridX == target.GridX && i.GridY == target.GridY);
        Assert.Equal(shared.MaxStack, filled.Stack);
    }
}
