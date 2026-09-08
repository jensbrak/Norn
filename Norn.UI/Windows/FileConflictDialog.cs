using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

/// <summary>
/// The modal prompt shown when a save is about to overwrite a file that
/// changed on disk since Norn loaded it — most plausibly Valheim itself,
/// mid-session. Same
/// Save/Discard/Cancel shape and <see cref="DirtyGuardChoice"/> result as
/// <see cref="UnsavedChangesDialog"/>, but not built on it directly — same
/// reasoning <see cref="ConfirmDialog"/>'s own doc comment already gives for
/// why that dialog isn't reused for a different flow: the scenario and the
/// stakes are genuinely different here (an existing newer version already on
/// disk, versus the user's own uncommitted edit), different enough to want
/// its own wording rather than a parameterized generalization forced onto a
/// three-button dialog that already has one real caller.
/// </summary>
internal sealed class FileConflictDialog : Window
{
    private DirtyGuardChoice _choice = DirtyGuardChoice.Cancel;

    private FileConflictDialog(string fileName)
    {
        Title = "File changed on disk";
        Icon = AppIcon.Default;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = $"\"{fileName}\" has changed on disk since Norn loaded it — most likely Valheim "
                + "itself, while it was running. Overwriting now would discard whatever changed there.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 360,
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button("Overwrite anyway", DirtyGuardChoice.Save));
        buttons.Children.Add(Button("Reload from disk", DirtyGuardChoice.Discard));
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
        var dialog = new FileConflictDialog(fileName);
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
