using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

/// <summary>
/// The sidebar's own control row: live search, sort key/direction, and the
/// "show backups" filter — everything <see cref="SaveFileListView.Apply"/>
/// needs, gathered from stock controls. Deliberately filenames-only in the
/// list itself (no per-property columns); full path/modified/backup status
/// live in each row's tooltip instead (see <c>MainWindow</c>).
/// <para>
/// Sort key/direction are remembered across launches via
/// <see cref="AppStateStore"/> — app-tracked state, not a
/// <see cref="Settings"/> preference (nobody configures a sort order in
/// advance, they just pick one while browsing and expect it to stick).
/// Search text and "Show backups" are deliberately <em>not</em> persisted —
/// both are transient view filters someone would want to start fresh each
/// launch, not settings they'd expect remembered.
/// </para>
/// </summary>
internal sealed class SaveFileListControls : StackPanel
{
    // Square, zeroed-padding, centered-content icon buttons — see
    // IconButtons for why (two Fluent-theme quirks found here originally).
    private readonly TextBox _search = new() { Watermark = "Search saves..." };
    private readonly Button _clearSearch = IconButtons.Create(IconButtons.ClearGlyph, "Clear the search filter.");
    // ComboBox's Fluent control theme sets its own default HorizontalAlignment
    // to Left, unlike every other control here (which inherit the normal
    // Stretch default) -- confirmed against the theme's own source and
    // AvaloniaUI/Avalonia#12762, closed by-design. Without this override it
    // sizes to its content and leaves the DockPanel's fill space empty
    // (found in manual QA -- and confirmed empty, not just unpainted: a
    // click there does nothing, since no control actually occupies it).
    private readonly ComboBox _sortKey = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _direction = IconButtons.Create("↓", "Toggle sort direction.");
    private readonly CheckBox _showBackups = new() { Content = "Show backups" };

    public event Action? Changed;

    public string SearchText => _search.Text ?? "";

    // Item order matches SaveFileSortKey's declaration order (Name=0, Modified=1) —
    // ComboBox.SelectedIndex is cast straight to the enum rather than mapped
    // through a lookup, since there are only the two entries.
    public SaveFileSortKey SortKey => (SaveFileSortKey)_sortKey.SelectedIndex;

    public bool Descending { get; private set; }

    public bool ShowBackups => _showBackups.IsChecked == true;

    public SaveFileListControls()
    {
        Orientation = Orientation.Vertical;
        Spacing = 4;
        Margin = new Avalonia.Thickness(4);

        _clearSearch.Margin = IconButtons.ButtonSpacing;
        _direction.Margin = IconButtons.ButtonSpacing;

        _sortKey.Items.Add("Name");
        _sortKey.Items.Add("Modified");
        // Remembered, not a fixed default — AppState.SidebarSortKey/
        // SidebarSortDescending persist whatever was last chosen, the same
        // way a window remembers its last size; this isn't a Settings.cs
        // preference, nobody configures a sort order in advance.
        _sortKey.SelectedIndex = (int)AppStateStore.Current.SidebarSortKey;
        Descending = AppStateStore.Current.SidebarSortDescending;
        UpdateDirectionGlyph();

        // DockPanel, not a horizontal StackPanel: the button docks to the
        // right edge and its sibling fills the remainder, so the button
        // lines up with the search box/file list's right edge instead of
        // trailing directly after whatever width the sibling naturally
        // wants (same pattern as StatusBar's dirty chip).
        var searchRow = new DockPanel();
        DockPanel.SetDock(_clearSearch, Dock.Right);
        searchRow.Children.Add(_clearSearch);
        searchRow.Children.Add(_search);

        var sortRow = new DockPanel();
        DockPanel.SetDock(_direction, Dock.Right);
        sortRow.Children.Add(_direction);
        sortRow.Children.Add(_sortKey);

        Children.Add(searchRow);
        Children.Add(sortRow);
        Children.Add(_showBackups);

        _search.TextChanged += (_, _) => Changed?.Invoke();
        _sortKey.SelectionChanged += (_, _) =>
        {
            AppStateStore.Current.SidebarSortKey = SortKey;
            AppStateStore.Save();
            Changed?.Invoke();
        };
        _showBackups.IsCheckedChanged += (_, _) => Changed?.Invoke();
        _clearSearch.Click += (_, _) => _search.Text = "";
        _direction.Click += (_, _) =>
        {
            Descending = !Descending;
            AppStateStore.Current.SidebarSortDescending = Descending;
            AppStateStore.Save();
            UpdateDirectionGlyph();
            Changed?.Invoke();
        };
    }

    private void UpdateDirectionGlyph() => _direction.Content = Descending ? "↓" : "↑";
}
