using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

internal enum DirtyGuardChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>
/// The modal prompt shown when switching away from a save with pending
/// edits. Avalonia has no built-in message box; this is the minimal
/// equivalent — three buttons over <see cref="Window.ShowDialog{TResult}"/>,
/// no XAML.
/// </summary>
internal sealed class UnsavedChangesDialog : Window
{
    private DirtyGuardChoice _choice = DirtyGuardChoice.Cancel;

    private UnsavedChangesDialog(string fileName)
    {
        Title = "Unsaved changes";
        Icon = AppIcon.Default;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"\"{fileName}\" has unsaved changes." });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button("Save", DirtyGuardChoice.Save));
        buttons.Children.Add(Button("Discard", DirtyGuardChoice.Discard));
        buttons.Children.Add(Button("Cancel", DirtyGuardChoice.Cancel));
        panel.Children.Add(buttons);

        Content = DialogChrome.Wrap(panel);
    }

    private Button Button(string label, DirtyGuardChoice choice)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) =>
        {
            _choice = choice;
            Close();
        };
        return button;
    }

    /// <summary>Shows the prompt modally over <paramref name="owner"/> and
    /// returns the user's choice. Closing the dialog any other way (e.g. the
    /// window's own close button) is treated as Cancel.</summary>
    internal static async Task<DirtyGuardChoice> Ask(Window owner, string fileName)
    {
        var dialog = new UnsavedChangesDialog(fileName);
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
