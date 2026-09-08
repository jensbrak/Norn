namespace Norn.UI;

public enum SaveFileSortKey
{
    Name,
    Modified,
}

/// <summary>
/// Computes the sidebar's visible list from the full discovered set. Pure
/// function — no Avalonia types, no I/O — so it's testable the same way as
/// <see cref="SaveDirectoryResolver"/>: feed it data, assert the result.
/// Recomputed wholesale on every relevant input change rather than mutated
/// in place, matching <c>MainWindow</c>'s existing recompute-and-reassign
/// style (see <c>RefreshSidebarLabels</c>).
/// </summary>
public static class SaveFileListView
{
    public static IReadOnlyList<SaveFileEntry> Apply(
        IReadOnlyList<SaveFileEntry> entries,
        string searchText,
        SaveFileSortKey sortKey,
        bool descending,
        bool showBackups)
    {
        IEnumerable<SaveFileEntry> visible = entries;

        if (!showBackups)
        {
            visible = visible.Where(entry => !entry.IsBackup);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            visible = visible.Where(entry => entry.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        visible = sortKey switch
        {
            SaveFileSortKey.Modified => visible.OrderBy(entry => entry.LastModified),
            _ => visible.OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase),
        };

        return descending ? visible.Reverse().ToList() : visible.ToList();
    }
}
