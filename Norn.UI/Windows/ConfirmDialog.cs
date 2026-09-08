using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

/// <summary>
/// Generic Yes/No modal confirmation — the reusable building block behind
/// one-off "are you sure" prompts app-wide (first consumer: the save-time
/// file-rename offer). Avalonia has no built-in message box; same minimal
/// pattern as <see cref="UnsavedChangesDialog"/> (no XAML, one
/// <see cref="Window.ShowDialog{TResult}"/> per call) — that dialog isn't
/// reused directly since its three choices (Save/Discard/Cancel) are
/// specific to the dirty-guard flow, not a general yes/no shape.
/// <c>*Dialog</c> vs. <c>*Window</c> naming (this file is the anchor
/// example for the former).
/// </summary>
internal sealed class ConfirmDialog : Window
{
    private bool _confirmed;

    private ConfirmDialog(string title, string message, string confirmLabel, string cancelLabel)
    {
        Title = title;
        Icon = AppIcon.Default;
        SizeToContent = SizeToContent.WidthAndHeight;
        // A short message + two small buttons can size narrower than what
        // the OS title bar needs to show its own title text without
        // clipping (found live: "Save changes" cropped at the
        // SizeToContent-driven ~204px). UnsavedChangesDialog never hits
        // this only because its third button happens to push it past that
        // point — not a deliberate width, so not a substitute for actually
        // setting one here.
        MinWidth = 280;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 360 });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var confirm = new Button { Content = confirmLabel };
        var cancel = new Button { Content = cancelLabel };
        confirm.Click += (_, _) => { _confirmed = true; Close(); };
        cancel.Click += (_, _) => { _confirmed = false; Close(); };
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = DialogChrome.Wrap(panel);
    }

    /// <summary>Shows the prompt modally over <paramref name="owner"/> and
    /// returns whether the confirm button was clicked. Closing any other way
    /// (e.g. the window's own close button) is treated as declined.</summary>
    internal static async Task<bool> Ask(Window owner, string title, string message, string confirmLabel = "Yes", string cancelLabel = "No")
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel, cancelLabel);
        await dialog.ShowDialog(owner);
        return dialog._confirmed;
    }
}
