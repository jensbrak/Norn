using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The "Add Item" picker — a real content surface (search + category filter
/// + list), so <c>*Window</c>, not <c>*Dialog</c>. Visually
/// modeled on the save-file sidebar's own search/filter row
/// (<see cref="SaveFileListControls"/>) as a functionality/layout reference,
/// not a literal port: a search box + clear button, a category
/// <see cref="ComboBox"/> in place of the sidebar's sort key/direction pair
/// (category isn't a sort axis, so there's no direction button), and two
/// convenience <see cref="CheckBox"/>es in place of "Show backups" — "Set
/// crafter tag" and "Fill stack" (quality/durability didn't seem worth
/// the same treatment).
/// <para>
/// Both checkboxes' *default* state (not their per-session value — see
/// below) comes from <see cref="Settings.DefaultSetCrafterTagOnAdd"/>/
/// <see cref="Settings.DefaultFillStackOnAdd"/>, not <see cref="AppState"/>
/// (moved off it, 2026-09-02) — the distinction: a deliberate,
/// stable, values-based default is a real preference someone would want to
/// find and set once, not silent "whatever I clicked last" bookkeeping,
/// which is what <see cref="AppState"/> is actually for (contrast the
/// sidebar's sort order, which stayed <see cref="AppState"/> for exactly
/// that reason — it's passive view continuity, not a behavioral default).
/// Both settings default <c>true</c> — "Set crafter tag" replicates real
/// game behavior and is
/// safe to default on now that it's per-item gated; "Fill stack" isn't
/// a personal preference but is judged the likely majority want.
/// Checking/unchecking a box in this window only affects *this* session,
/// same as search text/category — it does not write back to
/// <see cref="Settings"/>; that only changes through the Settings window
/// itself.
/// </para>
/// <para>
/// Both checkboxes are further gated per-item — "Set crafter tag" on
/// <see cref="SharedItemDataDto.CanHaveCrafterTag"/>, "Fill stack" on
/// <see cref="SharedItemDataDto.MaxStack"/>
/// <c>&gt; 1</c> — disabled and force-unchecked whenever the currently
/// selected item doesn't support the action, regardless of the default
/// setting or what was checked for the previously selected item. Real
/// restriction (disabled), not explanatory wording — "Fill stack (when
/// possible)" was this window's own earlier attempt at the wording approach
/// instead, reverted 2026-09-02 once "Set crafter tag" needed the real
/// restriction anyway (the whole point being to stop Norn from quietly
/// producing a state the actual game could never itself produce; a
/// same-window sibling checkbox silently no-op'ing right next to it read as
/// exactly the inconsistency that would undersell it) — both now behave
/// identically, and the plain "Fill stack" label no longer needs the
/// qualifier once disabling already says the same thing more directly.
/// </para>
/// <para>
/// No drag-and-drop (Loki's own add-item mechanism) — deliberately avoided,
/// matching the inventory tile UI's existing stance (Shift-click
/// stack-split was dropped for implying a drag-drop landing mechanism Norn
/// has nowhere else). Picking a row adds it and closes the window, via
/// either a double-click or the explicit "Add" button — both do the same
/// thing, framing the button as a second way to
/// trigger one action, not a separate multi-add mode.
/// </para>
/// </summary>
internal sealed class AddItemWindow : Window
{
    private readonly IReadOnlyList<SharedItemDataDto> _allItems = SharedItemDataCatalog.All.ToList();

    // Index-aligned with _category's own Items — "All" (null) first, then
    // one entry per distinct ItemType actually present in the catalog (not
    // the full 24-member enum: an empty category would be a selectable dead
    // end), sorted by the same humanized label the combo displays.
    private readonly List<ItemType?> _categoryValues = [];

    private readonly TextBox _search = new() { Watermark = "Search items..." };
    private readonly Button _clearSearch = IconButtons.Create(IconButtons.ClearGlyph, "Clear the search filter.");
    private readonly ComboBox _category = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _setCrafterTag = new() { Content = "Set crafter tag", IsEnabled = false };
    private readonly CheckBox _fillStack = new() { Content = "Fill stack", IsEnabled = false };
    private readonly ListBox _list = new();
    private readonly Button _add = new() { Content = "Add", IsEnabled = false };

    private (string PrefabName, bool SetCrafter, bool FillStack)? _result;

    private AddItemWindow()
    {
        Title = "Add Item";
        Icon = AppIcon.Default;
        Width = 420;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _categoryValues.Add(null);
        _category.Items.Add("All categories");
        foreach (var type in _allItems.Select(i => i.ItemType).Distinct()
                     .OrderBy(t => TabRows.Humanize(t.ToString()), StringComparer.OrdinalIgnoreCase))
        {
            _categoryValues.Add(type);
            _category.Items.Add(TabRows.Humanize(type.ToString()));
        }

        _category.SelectedIndex = 0;
        // Both checkboxes start disabled/unchecked (constructor initializers) regardless
        // of either setting's default — nothing is selected yet, so nothing is known to
        // support either action. SelectionChanged (below) applies the real defaults the
        // first time a selection actually exists.

        // supportsRecycling: false — same reason as MainWindow's sidebar
        // template (found in review): the row's text is snapshotted at
        // construction with no binding to re-evaluate, so a recycled control
        // keeps the previous item's display name while the selection moves
        // on, which here decides which item actually gets added. Null-guarded
        // for the same reason as that one too — Avalonia calls the factory
        // with a null item during some container generation passes, which
        // disabling recycling makes reachable.
        _list.ItemTemplate = new FuncDataTemplate<AddItemRow>(
            (row, _) => new TextBlock { Text = row?.Label ?? string.Empty },
            supportsRecycling: false);

        var searchRow = new DockPanel();
        _clearSearch.Margin = IconButtons.ButtonSpacing;
        DockPanel.SetDock(_clearSearch, Dock.Right);
        searchRow.Children.Add(_clearSearch);
        searchRow.Children.Add(_search);

        // Side by side, not one checkbox per row — two short labels don't
        // each need a full row.
        var checkboxRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        checkboxRow.Children.Add(_setCrafterTag);
        checkboxRow.Children.Add(_fillStack);

        var topStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(12, 12, 12, 8) };
        topStack.Children.Add(searchRow);
        topStack.Children.Add(_category);
        topStack.Children.Add(checkboxRow);
        DockPanel.SetDock(topStack, Dock.Top);

        var cancel = new Button { Content = "Cancel" };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 8, 12, 12),
        };
        buttons.Children.Add(_add);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);

        _list.Margin = new Thickness(12, 0, 12, 0);

        var root = new DockPanel();
        root.Children.Add(topStack);
        root.Children.Add(buttons);
        root.Children.Add(_list); // fills the remainder (DockPanel.LastChildFill)

        Content = DialogChrome.Wrap(root);

        _search.TextChanged += (_, _) => Refresh();
        _category.SelectionChanged += (_, _) => Refresh();
        _clearSearch.Click += (_, _) => _search.Text = "";
        _list.SelectionChanged += (_, _) =>
        {
            _add.IsEnabled = _list.SelectedItem is not null;

            // Real per-item restriction, not explanatory wording (see this class's
            // own doc comment) — both disabled and force-unchecked whenever the
            // selected item doesn't support the action, applying each Setting's own
            // default only when the item actually does.
            var selected = (_list.SelectedItem as AddItemRow)?.Item;

            var canTag = selected?.CanHaveCrafterTag == true;
            _setCrafterTag.IsEnabled = canTag;
            _setCrafterTag.IsChecked = canTag && SettingsStore.Current.DefaultSetCrafterTagOnAdd;

            var canFillStack = selected is { MaxStack: > 1 };
            _fillStack.IsEnabled = canFillStack;
            _fillStack.IsChecked = canFillStack && SettingsStore.Current.DefaultFillStackOnAdd;
        };
        _list.DoubleTapped += (_, _) => Confirm();
        _add.Click += (_, _) => Confirm();
        cancel.Click += (_, _) => Close();

        Refresh();
    }

    private void Refresh()
    {
        _list.ItemsSource = AddItemPickerView.Apply(_allItems, _search.Text ?? "", _categoryValues[_category.SelectedIndex]);
    }

    private void Confirm()
    {
        if (_list.SelectedItem is not AddItemRow row)
        {
            return;
        }

        _result = (row.Item.ItemName, _setCrafterTag.IsChecked == true, _fillStack.IsChecked == true);
        Close();
    }

    /// <summary>Shows the picker modally over <paramref name="owner"/> and
    /// returns the chosen prefab name plus whether to also stamp it as
    /// crafted by the current profile and/or fill its stack to the catalog
    /// max (a no-op for a non-stackable item — <see cref="CharacterEditor.FillItemStack"/>
    /// already degrades gracefully), or <c>null</c> if cancelled (any way
    /// other than Add/double-click, same "closing is declining" convention
    /// as <see cref="ConfirmDialog.Ask"/>).</summary>
    internal static async Task<(string PrefabName, bool SetCrafter, bool FillStack)?> Open(Window owner)
    {
        var window = new AddItemWindow();
        await window.ShowDialog(owner);
        return window._result;
    }
}
