using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The "Add Item" picker — a real content surface (search + category filter
/// + list), so <c>*Window</c>, not <c>*Dialog</c>. Visually
/// modeled on the save-file sidebar's own search/filter row
/// (<see cref="SaveFileListControls"/>) as a functionality/layout reference,
/// not a literal port: a search box + clear button, a category
/// <see cref="ComboBox"/> in place of the sidebar's sort key/direction pair
/// (category isn't a sort axis, so there's no direction button), a
/// convenience <see cref="CheckBox"/> in place of "Show backups" — "Set
/// crafter tag" — and an amount field (quality/durability didn't seem worth
/// the same treatment).
/// <para>
/// The amount field is a plain <see cref="NumericUpDown"/> —
/// <c>Minimum</c>/<c>Maximum</c>/<c>Value</c> only, no
/// <c>InnerRightContent</c> trick. That trick (an inline "/ M" reading as
/// part of the same control) was exactly one of the chrome problems found
/// with <c>StackEditDialog</c>, a predecessor of this window — the item's
/// max stack is shown here as ordinary separate static text in the field's
/// own label instead
/// ("Amount (max 50):"), not embedded inside the control. The control's own
/// <c>Maximum</c> is deliberately <see cref="AmountEntry.Ceiling"/>, not the
/// selected item's real max — see that constant's own doc comment for why a
/// lower, per-item <c>Maximum</c> here reintroduces a live keystroke-
/// rejection bug; the real bound is enforced once, correctly, at Add time by
/// <see cref="CharacterEditor.SetItemStack"/>'s own clamp.
/// </para>
/// <para>
/// The checkbox's and the amount field's *default* state (not their
/// per-session value — see below) come from
/// <see cref="Settings.DefaultSetCrafterTagOnAdd"/>/
/// <see cref="Settings.DefaultAmountToMaxOnAdd"/>, not <see cref="AppState"/>
/// (moved off it, 2026-09-02) — the distinction: a deliberate,
/// stable, values-based default is a real preference someone would want to
/// find and set once, not silent "whatever I clicked last" bookkeeping,
/// which is what <see cref="AppState"/> is actually for (contrast the
/// sidebar's sort order, which stayed <see cref="AppState"/> for exactly
/// that reason — it's passive view continuity, not a behavioral default).
/// Both settings default <c>true</c> — "Set crafter tag" replicates real
/// game behavior and is
/// safe to default on now that it's per-item gated; defaulting the amount
/// to max isn't a personal preference either but is judged the likely
/// majority want.
/// Changing either in this window only affects *this* session,
/// same as search text/category — it does not write back to
/// <see cref="Settings"/>; that only changes through the Settings window
/// itself.
/// </para>
/// <para>
/// Both are further gated per-item — "Set crafter tag" on
/// <see cref="SharedItemDataDto.CanHaveCrafterTag"/>, the amount field on
/// <see cref="SharedItemDataDto.MaxStack"/>
/// <c>&gt; 1</c> — disabled (and, for the amount field, pinned to 1) whenever
/// the currently selected item doesn't support the action, regardless of the
/// default setting or what was set for the previously selected item. Real
/// restriction (disabled), not explanatory wording — "Fill stack (when
/// possible)" was this window's own earlier attempt at the wording approach
/// instead, reverted 2026-09-02 once "Set crafter tag" needed the real
/// restriction anyway (the whole point being to stop Norn from quietly
/// producing a state the actual game could never itself produce; a
/// same-window sibling control silently no-op'ing right next to it read as
/// exactly the inconsistency that would undersell it) — both now behave
/// identically.
/// </para>
/// <para>
/// No drag-and-drop (Loki's own add-item mechanism) — deliberately avoided,
/// matching the inventory tile UI's existing stance (Shift-click
/// stack-split was dropped for implying a drag-drop landing mechanism Norn
/// has nowhere else). Picking a row (double-click or the "Add" button — both
/// trigger the same action) invokes the caller's <c>onPick</c> callback
/// immediately, which performs the mutation and reports back whether room
/// remains for another add; the window itself never touches
/// <see cref="Norn.Adapter.CharacterEditor"/>.
/// </para>
/// <para>
/// **Keep window open.** An opt-in <see cref="CheckBox"/>, shown only when
/// the caller passes <c>allowKeepOpen: true</c> (the toolbar "Add items"
/// entry point; the empty tile's own single-slot "Add item" context-menu
/// entry never shows it — multi-add doesn't make sense targeting one
/// specific tile). Checked and room still remaining after a pick: the
/// window stays open instead of closing, selection/search/category/checkbox
/// state untouched, so a repeated Add or double-click adds again
/// immediately. Unchecked, or no room left, or Close: closes, same as
/// before this existed. Its value persists via
/// <see cref="AppState.AddItemKeepWindowOpen"/> — resumed on open, saved on
/// every toggle, same mechanism <c>WorldMapWindow</c>'s own two checkboxes
/// use — not <see cref="Settings"/>: whether someone happens to be
/// batch-adding right now isn't a values-based preference a genuinely
/// different user would want stable and findable, just workflow continuity
/// worth resuming silently, unlike "Set crafter tag"/the amount field's own
/// Settings-backed *defaults* just above (which still reset fresh
/// per-session regardless). Because picks now commit immediately rather
/// than after <see cref="Open"/> returns, the dismiss button reads "Close"
/// here instead of "Cancel" — any earlier picks in a keep-open session
/// already happened and aren't undone by dismissing the window, so framing
/// it as declining would be misleading. The anchored, single-shot path
/// keeps "Cancel": nothing commits there until a pick is actually made, so
/// the "closing is declining" convention (<see cref="ConfirmDialog.Ask"/>'s
/// own framing) still holds exactly.
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
    // The AmountLabelClass tag + the Style added in the constructor is
    // what actually dims this on disable — see that Style's own comment.
    private const string AmountLabelClass = "amount-label";
    private readonly TextBlock _amountLabel = new() { Text = "Amount:", VerticalAlignment = VerticalAlignment.Center, IsEnabled = false, Classes = { AmountLabelClass } };
    private readonly NumericUpDown _amount = new() { Minimum = 1, Maximum = AmountEntry.Ceiling, Value = 1, IsEnabled = false, Width = 130 };
    private readonly ListBox _list = new();
    private readonly Button _add = new() { Content = "Add", IsEnabled = false };
    private readonly CheckBox _keepOpen = new() { Content = "Keep window open" };

    private readonly bool _allowKeepOpen;
    private readonly Func<(string PrefabName, bool SetCrafter, int Amount), bool> _onPick;

    private AddItemWindow(bool allowKeepOpen, Func<(string PrefabName, bool SetCrafter, int Amount), bool> onPick)
    {
        _allowKeepOpen = allowKeepOpen;
        _onPick = onPick;

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
        // Both the checkbox and the amount field start disabled (constructor
        // initializers) regardless of either setting's default — nothing is
        // selected yet, so nothing is known to support either action.
        // SelectionChanged (below) applies the real defaults the first time
        // a selection actually exists.

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

        // Side by side, not one control per row — the checkbox and the
        // amount group don't each need a full row.
        var amountGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        amountGroup.Children.Add(_amountLabel);
        amountGroup.Children.Add(_amount);

        var checkboxRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        checkboxRow.Children.Add(_setCrafterTag);
        checkboxRow.Children.Add(amountGroup);

        var topStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(12, 12, 12, 8) };
        topStack.Children.Add(searchRow);
        topStack.Children.Add(_category);
        topStack.Children.Add(checkboxRow);
        DockPanel.SetDock(topStack, Dock.Top);

        // "Close" once allowKeepOpen is true — a pick commits immediately
        // (see Confirm below), so "Cancel" would misdescribe dismissing the
        // window after one or more picks already happened. The anchored,
        // single-shot path keeps "Cancel": nothing commits there until a
        // pick is made, so "closing is declining" still holds.
        var closeButton = new Button { Content = allowKeepOpen ? "Close" : "Cancel" };
        var rightButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        rightButtons.Children.Add(_add);
        rightButtons.Children.Add(closeButton);

        // A DockPanel, not a single StackPanel, so "Keep window open" can
        // sit at the far left while Add/Close stay right-aligned in the
        // remaining space — same right-aligned look as before this checkbox
        // existed when allowKeepOpen is false and it's never added.
        var bottomRow = new DockPanel { Margin = new Thickness(12, 8, 12, 12) };
        if (allowKeepOpen)
        {
            _keepOpen.IsChecked = AppStateStore.Current.AddItemKeepWindowOpen;
            _keepOpen.VerticalAlignment = VerticalAlignment.Center;
            _keepOpen.IsCheckedChanged += (_, _) =>
            {
                AppStateStore.Current.AddItemKeepWindowOpen = _keepOpen.IsChecked == true;
                AppStateStore.Save();
            };
            DockPanel.SetDock(_keepOpen, Dock.Left);
            bottomRow.Children.Add(_keepOpen);
        }
        bottomRow.Children.Add(rightButtons); // fills the remainder (DockPanel.LastChildFill)
        DockPanel.SetDock(bottomRow, Dock.Bottom);

        _list.Margin = new Thickness(12, 0, 12, 0);

        var root = new DockPanel();
        root.Children.Add(topStack);
        root.Children.Add(bottomRow);
        root.Children.Add(_list); // fills the remainder (DockPanel.LastChildFill)

        Content = DialogChrome.Wrap(root);

        // Mirrors CheckBox's own disabled-state mechanism for a plain
        // TextBlock, which has none of its own — confirmed against
        // Avalonia's actual Fluent theme source (11.3.19), not guessed:
        // CheckBox.xaml's ":disabled" style swaps Foreground to
        // {DynamicResource CheckBoxForegroundUncheckedDisabled}, which
        // FluentControlResources.xaml in turn points at
        // SystemControlDisabledBaseMediumLowBrush — the same shared token
        // Button/ComboBox/RepeatButton all use for their own disabled text.
        // Declared as a real Style with a ":disabled" selector, exactly
        // like CheckBox's own, rather than an imperative C# resource
        // lookup: IsEnabled already flips the ":disabled" pseudo-class on
        // every Control for free, so the Style applies/reverts
        // automatically and correctly whenever _amountLabel.IsEnabled
        // changes below — no manual TryFindResource/ClearValue timing to
        // get right (an earlier version of this fix tried exactly that in
        // code-behind and got the timing wrong, leaving the label
        // unreadable when caught during live testing).
        Styles.Add(new Style(x => x.OfType<TextBlock>().Class(AmountLabelClass).Class(":disabled"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("SystemControlDisabledBaseMediumLowBrush")) },
        });

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

            // Maximum stays fixed at AmountEntry.Ceiling regardless of the
            // selected item's real MaxStack — see that constant's own doc
            // comment for why (a lower, per-item Maximum here reintroduces
            // NumericUpDown's live keystroke-rejection bug). The real bound
            // is still enforced, correctly, once at Add time by
            // CharacterEditor.SetItemStack's own clamp.
            var canStack = selected is { MaxStack: > 1 };
            _amountLabel.Text = canStack ? $"Amount (max {selected!.MaxStack}):" : "Amount:";
            _amountLabel.IsEnabled = canStack;
            _amount.IsEnabled = canStack;
            _amount.Value = canStack && SettingsStore.Current.DefaultAmountToMaxOnAdd ? selected!.MaxStack : 1;
        };
        _list.DoubleTapped += (_, _) => Confirm();
        _add.Click += (_, _) => Confirm();
        closeButton.Click += (_, _) => Close();

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

        var pick = (row.Item.ItemName, _setCrafterTag.IsChecked == true, (int)(_amount.Value ?? 1));
        var roomRemains = _onPick(pick);

        if (_allowKeepOpen && _keepOpen.IsChecked == true && roomRemains)
        {
            // Stay open: selection/search/category/checkbox state is left
            // exactly as it is, so a repeated Add or double-click adds again
            // immediately.
            return;
        }

        Close();
    }

    /// <summary>Shows the picker modally over <paramref name="owner"/>.
    /// Each pick (double-click or the "Add" button — both trigger the same
    /// action) invokes <paramref name="onPick"/> immediately with the chosen
    /// prefab name, whether to also stamp it as crafted by the current
    /// profile, and the chosen amount (always <c>&gt;= 1</c>; stays 1 for a
    /// non-stackable item, and setting it above 1 is a no-op for one anyway —
    /// <see cref="CharacterEditor.SetItemStack"/> already degrades
    /// gracefully). <paramref name="onPick"/> performs the mutation and
    /// returns whether room remains for another add; when
    /// <paramref name="allowKeepOpen"/> is true and its own "Keep window
    /// open" checkbox is checked, the window stays open to pick again as
    /// long as room remains, instead of closing after one pick.</summary>
    internal static async Task Open(Window owner, bool allowKeepOpen, Func<(string PrefabName, bool SetCrafter, int Amount), bool> onPick)
    {
        var window = new AddItemWindow(allowKeepOpen, onPick);
        await window.ShowDialog(owner);
    }
}
