using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="SaveFileListView"/>'s pure filter/sort pipeline.
/// </summary>
/// <remarks>
/// The fixture order below is deliberately neither alphabetical nor
/// chronological, and the name and date orderings deliberately disagree with
/// each other (found in review). Previously the source list was already in
/// both name-ascending and date-ascending order, so every sorting
/// expectation could be satisfied by an implementation that ignored the sort
/// key entirely and merely reversed the input for <c>descending</c> — the
/// tests could not distinguish that from a correct one.
/// </remarks>
public class SaveFileListViewTests
{
    private static readonly SaveFileEntry Alice = new(@"C:\saves\alice.fch", "alice", new DateTime(2026, 2, 1), IsBackup: false);
    private static readonly SaveFileEntry Bob = new(@"C:\saves\bob.fch", "bob", new DateTime(2026, 9, 1), IsBackup: false);
    private static readonly SaveFileEntry Carol = new(@"C:\saves\carol.fch", "carol", new DateTime(2026, 5, 1), IsBackup: false);
    private static readonly SaveFileEntry AliceBackup = new(@"C:\saves\alice_backup_auto-20260301120000.fch", "alice_backup_auto-20260301120000", new DateTime(2026, 3, 1), IsBackup: true);

    // Source order: Carol, Alice, Bob — matches none of the four expected
    // orderings, nor the reverse of any of them.
    private static readonly IReadOnlyList<SaveFileEntry> All = [Carol, Alice, Bob, AliceBackup];

    [Fact]
    public void Backups_are_hidden_by_default()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Name, descending: false, showBackups: false);

        Assert.DoesNotContain(AliceBackup, visible);
        Assert.Equal(3, visible.Count);
    }

    [Fact]
    public void Backups_appear_when_requested()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Name, descending: false, showBackups: true);

        Assert.Contains(AliceBackup, visible);
        Assert.Equal(4, visible.Count);
    }

    [Fact]
    public void Search_filters_case_insensitively_by_file_name()
    {
        var visible = SaveFileListView.Apply(All, "ALI", SaveFileSortKey.Name, descending: false, showBackups: false);

        Assert.Equal([Alice], visible);
    }

    [Fact]
    public void Sorts_by_name_ascending()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Name, descending: false, showBackups: false);

        Assert.Equal([Alice, Bob, Carol], visible);
    }

    [Fact]
    public void Sorts_by_name_descending()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Name, descending: true, showBackups: false);

        Assert.Equal([Carol, Bob, Alice], visible);
    }

    [Fact]
    public void Sorts_by_modified_ascending()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Modified, descending: false, showBackups: false);

        Assert.Equal([Alice, Carol, Bob], visible);
    }

    [Fact]
    public void Sorts_by_modified_descending()
    {
        var visible = SaveFileListView.Apply(All, "", SaveFileSortKey.Modified, descending: true, showBackups: false);

        Assert.Equal([Bob, Carol, Alice], visible);
    }

    [Fact]
    public void Empty_search_text_matches_everything()
    {
        var visible = SaveFileListView.Apply(All, "   ", SaveFileSortKey.Name, descending: false, showBackups: true);

        Assert.Equal(4, visible.Count);
    }
}
