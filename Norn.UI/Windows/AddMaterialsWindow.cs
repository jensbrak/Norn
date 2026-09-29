using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>One pick from <see cref="AddMaterialsWindow"/>: what was chosen, and the
/// materials it comes to.</summary>
internal sealed record MaterialsPick(
    string DisplayName,
    bool HasLevels,
    int Level,
    bool UpgradeOnly,
    int Times,
    IReadOnlyList<ItemAmount> Materials);

/// <summary>
/// The "Add materials" picker: pick a recipe or a build piece, get the
/// materials it needs. Same layout and behavior as <see cref="AddItemWindow"/>
/// — search, category, list, "Keep window open" — with a row of options and a
/// preview of exactly what Add will put in the inventory.
/// <para>
/// <b>Level</b> is the quality the materials are for, enabled only for a
/// recipe whose output has quality levels. <b>Upgrade only</b> narrows that to
/// the single upgrade step to the chosen level; unchecked, it's the craft plus
/// every upgrade up to it. <b>Times</b> multiplies the lot. All of it is
/// <see cref="CraftingCosts"/>.
/// </para>
/// <para>
/// Add is disabled while the preview says the materials won't fit: the add is
/// all-or-nothing (<see cref="CharacterEditor.AddItemBundle"/>), and the
/// window never touches <see cref="CharacterEditor"/> itself — fitting and
/// adding both go through the caller's callbacks.
/// </para>
/// </summary>
internal sealed class AddMaterialsWindow : Window
{
    private readonly IReadOnlyList<RecipeDto> _recipes = RecipeCatalog.All;
    private readonly IReadOnlyList<PieceDto> _pieces = PieceCatalog.All;

    // Index-aligned with _category's items; null for the two group headers,
    // which are disabled and so never selected.
    private readonly List<MaterialsFilter?> _filters = [];

    private readonly TextBox _search = new() { Watermark = "Search recipes and build pieces..." };
    private readonly Button _clearSearch = IconButtons.Create(IconButtons.ClearGlyph, "Clear the search filter.");
    private readonly ComboBox _category = new() { HorizontalAlignment = HorizontalAlignment.Stretch };

    // See AddItemWindow's AmountLabelClass: TextBlock has no disabled look of its own.
    private const string OptionLabelClass = "option-label";
    private readonly TextBlock _levelLabel = OptionLabel("Level:");
    private readonly NumericUpDown _level = AmountEntry.Integer(new() { Minimum = 1, Maximum = 1, Value = 1, IsEnabled = false, Width = 110 });
    private readonly CheckBox _upgradeOnly = new() { Content = "Upgrade only", IsEnabled = false };
    private readonly TextBlock _timesLabel = OptionLabel("Times:");
    private readonly NumericUpDown _times = AmountEntry.Integer(new() { Minimum = 1, Maximum = AmountEntry.Ceiling, Value = 1, IsEnabled = false, Width = 130 });

    private readonly ListBox _list = new();
    private readonly TextBlock _preview = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _add = new() { Content = "Add", IsEnabled = false };
    private readonly CheckBox _keepOpen = new() { Content = "Keep window open" };

    private readonly Func<MaterialsPick, bool> _onPick;
    private readonly Func<IReadOnlyList<ItemAmount>, bool> _canFit;

    private AddMaterialsWindow(Func<MaterialsPick, bool> onPick, Func<IReadOnlyList<ItemAmount>, bool> canFit)
    {
        _onPick = onPick;
        _canFit = canFit;

        Title = "Add materials";
        Icon = AppIcon.Default;
        Width = 520;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildCategories();

        // supportsRecycling: false — see AddItemWindow: a recycled row keeps
        // its old text while the selection moves on.
        _list.ItemTemplate = new FuncDataTemplate<MaterialsRow>(
            (row, _) => new TextBlock { Text = row?.Label ?? string.Empty },
            supportsRecycling: false);

        var searchRow = new DockPanel();
        _clearSearch.Margin = IconButtons.ButtonSpacing;
        DockPanel.SetDock(_clearSearch, Dock.Right);
        searchRow.Children.Add(_clearSearch);
        searchRow.Children.Add(_search);

        // Times anchored to the right edge; Level and Upgrade only share the left.
        var timesField = LabeledField(_timesLabel, _times);
        DockPanel.SetDock(timesField, Dock.Right);
        var levelGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        levelGroup.Children.Add(LabeledField(_levelLabel, _level));
        levelGroup.Children.Add(_upgradeOnly);
        var optionsRow = new DockPanel();
        optionsRow.Children.Add(timesField);
        optionsRow.Children.Add(levelGroup);

        var topStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(12, 12, 12, 8) };
        topStack.Children.Add(searchRow);
        topStack.Children.Add(_category);
        topStack.Children.Add(optionsRow);
        DockPanel.SetDock(topStack, Dock.Top);

        var closeButton = new Button { Content = "Close" };
        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        rightButtons.Children.Add(_add);
        rightButtons.Children.Add(closeButton);

        _keepOpen.IsChecked = AppStateStore.Current.AddMaterialsKeepWindowOpen;
        _keepOpen.VerticalAlignment = VerticalAlignment.Center;
        _keepOpen.IsCheckedChanged += (_, _) =>
        {
            AppStateStore.Current.AddMaterialsKeepWindowOpen = _keepOpen.IsChecked == true;
            AppStateStore.Save();
        };

        var bottomRow = new DockPanel { Margin = new Thickness(12, 8, 12, 12) };
        DockPanel.SetDock(_keepOpen, Dock.Left);
        bottomRow.Children.Add(_keepOpen);
        bottomRow.Children.Add(rightButtons);
        DockPanel.SetDock(bottomRow, Dock.Bottom);

        // Fixed height, so picking something with more ingredients doesn't
        // resize the list above it.
        var previewScroll = new ScrollViewer { Content = _preview, Height = 150, Margin = new Thickness(12, 8, 12, 0) };
        DockPanel.SetDock(previewScroll, Dock.Bottom);

        _list.Margin = new Thickness(12, 0, 12, 0);

        var root = new DockPanel();
        root.Children.Add(topStack);
        root.Children.Add(bottomRow);
        root.Children.Add(previewScroll);
        root.Children.Add(_list); // fills the remainder

        Content = DialogChrome.Wrap(root);

        Styles.Add(new Style(x => x.OfType<TextBlock>().Class(OptionLabelClass).Class(":disabled"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("SystemControlDisabledBaseMediumLowBrush")) },
        });

        _search.TextChanged += (_, _) => Refresh();
        _category.SelectionChanged += (_, _) => Refresh();
        _clearSearch.Click += (_, _) => _search.Text = "";
        _list.SelectionChanged += (_, _) => OnSelectionChanged();
        _level.ValueChanged += (_, _) => { UpdateUpgradeOnly(); UpdatePreview(); };
        _upgradeOnly.IsCheckedChanged += (_, _) => UpdatePreview();
        _times.ValueChanged += (_, _) => UpdatePreview();
        _list.DoubleTapped += (_, _) => Confirm();
        _add.Click += (_, _) => Confirm();
        closeButton.Click += (_, _) => Close();

        Refresh();
    }

    // "All", then each kind under a disabled header: its "All ..." entry and
    // the categories (recipes) or groups (pieces) actually present.
    private void BuildCategories()
    {
        var all = AddMaterialsPickerView.Apply(_recipes, _pieces, SharedItemDataCatalog.TryFind, LocalizationCatalog.TryFind, "", new());

        void Add(string text, MaterialsFilter? filter)
        {
            _category.Items.Add(new ComboBoxItem { Content = text, IsEnabled = filter is not null });
            _filters.Add(filter);
        }

        Add("All", new());
        Add("── Recipes ──", null);
        Add("All recipes", new(MaterialsKind.Recipe));
        foreach (var type in all.OfType<RecipeRow>().Select(r => r.Item.ItemType).Distinct()
                     .OrderBy(t => TabRows.Humanize(t.ToString()), StringComparer.OrdinalIgnoreCase))
        {
            Add(TabRows.Humanize(type.ToString()), new(MaterialsKind.Recipe, ItemType: type));
        }

        Add("── Build pieces ──", null);
        Add("All build pieces", new(MaterialsKind.Piece));
        foreach (var group in all.OfType<PieceRow>().Select(p => AddMaterialsPickerView.PieceGroup(p.Piece)).Distinct()
                     .OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
        {
            Add(group, new(MaterialsKind.Piece, PieceGroup: group));
        }

        _category.SelectedIndex = 0;
    }

    private MaterialsRow? Selected => _list.SelectedItem as MaterialsRow;

    private int Level => (int)(_level.Value ?? _level.Minimum);

    private int Times => (int)(_times.Value ?? 1);

    private void Refresh()
    {
        var filter = _filters[Math.Max(0, _category.SelectedIndex)] ?? new();
        _list.ItemsSource = AddMaterialsPickerView.Apply(_recipes, _pieces, SharedItemDataCatalog.TryFind, LocalizationCatalog.TryFind, _search.Text ?? "", filter);
        OnSelectionChanged();
    }

    // Level resets to the recipe's first level on every new selection; a piece
    // has no levels. Times carries over, like search text and category.
    private void OnSelectionChanged()
    {
        var selected = Selected;
        _timesLabel.IsEnabled = _times.IsEnabled = selected is not null;

        var (first, max) = selected is RecipeRow r
            ? (CraftingCosts.FirstLevel(r.Recipe), Math.Max(CraftingCosts.FirstLevel(r.Recipe), r.Item.MaxQuality))
            : (1, 1);

        // Widen before narrowing, so no intermediate Minimum > Maximum.
        _level.Minimum = 1;
        _level.Maximum = max;
        _level.Minimum = first;
        _level.Value = first;
        _levelLabel.IsEnabled = _level.IsEnabled = max > first;

        UpdateUpgradeOnly();
        UpdatePreview();
    }

    // Only meaningful above the recipe's first level: there, the "single
    // step" and "everything up to here" differ.
    private void UpdateUpgradeOnly()
    {
        var enabled = Selected is RecipeRow && Level > _level.Minimum;
        _upgradeOnly.IsEnabled = enabled;
        if (!enabled)
        {
            _upgradeOnly.IsChecked = false;
        }
    }

    private IReadOnlyList<ItemAmount> CurrentMaterials(MaterialsRow? row) => row switch
    {
        RecipeRow r => CraftingCosts.Materials(r.Recipe, Level, _upgradeOnly.IsChecked == true, Times),
        PieceRow p => CraftingCosts.Materials(p.Piece, Times),
        _ => [],
    };

    // The same four labels whether or not anything is selected, so the
    // preview shows what to expect before a pick and never changes shape.
    private void UpdatePreview()
    {
        var row = Selected;
        var materials = CurrentMaterials(row);
        var fits = materials.Count > 0 && _canFit(materials);
        var (station, stationToken, season) = row switch
        {
            RecipeRow r => (r.Recipe.Station, r.Recipe.StationToken, r.Recipe.Season),
            PieceRow p => (p.Piece.Station, p.Piece.StationToken, p.Piece.Season),
            _ => (null, null, null),
        };

        var lines = new List<string> { "Adds:" };
        lines.AddRange(materials.Select(m => $"    {SharedItemDataCatalog.TryFind(m.ItemName)?.DisplayName ?? m.ItemName} ×{m.Amount}"));
        lines.Add($"Station: {(row is null ? "" : stationToken is null ? "None" : LocalizationCatalog.TryFind(stationToken) ?? station ?? stationToken)}");
        lines.Add($"Season: {season}");
        lines.Add($"Fits inventory: {(row is null ? "" : fits ? "Yes" : "No")}");

        _preview.Text = string.Join('\n', lines);
        _add.IsEnabled = fits;
    }

    private void Confirm()
    {
        if (Selected is not { } row || !_add.IsEnabled)
        {
            return;
        }

        var pick = new MaterialsPick(row.Label, _level.IsEnabled, Level, _upgradeOnly.IsChecked == true, Times, CurrentMaterials(row));
        var roomRemains = _onPick(pick);

        if (_keepOpen.IsChecked == true && roomRemains)
        {
            // Stay open with everything as it was; only whether the same
            // pick still fits has changed.
            UpdatePreview();
            return;
        }

        Close();
    }

    private static TextBlock OptionLabel(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false, Classes = { OptionLabelClass } };

    private static StackPanel LabeledField(TextBlock label, Control field)
    {
        var group = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        group.Children.Add(label);
        group.Children.Add(field);
        return group;
    }

    /// <summary>Shows the picker modally over <paramref name="owner"/>. Each
    /// pick invokes <paramref name="onPick"/> immediately, which adds the
    /// materials and returns whether room remains for another add;
    /// <paramref name="canFit"/> drives the preview's fit line and Add's
    /// enabled state.</summary>
    internal static async Task Open(Window owner, Func<MaterialsPick, bool> onPick, Func<IReadOnlyList<ItemAmount>, bool> canFit)
    {
        var window = new AddMaterialsWindow(onPick, canFit);
        await window.ShowDialog(owner);
    }
}
