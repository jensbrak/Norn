using Avalonia.Controls;
using Avalonia.Layout;

namespace Norn.UI;

/// <summary>
/// Generic single-button informational/error modal — the OK-only
/// counterpart to <see cref="ConfirmDialog"/>, for surfacing a fact the user
/// must acknowledge (e.g. why an automatic file rename couldn't happen)
/// rather than a choice.
/// </summary>
internal sealed class MessageDialog : Window
{
    private MessageDialog(string title, string message)
    {
        Title = title;
        Icon = AppIcon.Default;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Avalonia.Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 360 });

        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();
        panel.Children.Add(ok);

        Content = DialogChrome.Wrap(panel);
    }

    /// <summary>Shows the message modally over <paramref name="owner"/> and
    /// waits for it to be dismissed.</summary>
    internal static Task Show(Window owner, string title, string message)
        => new MessageDialog(title, message).ShowDialog(owner);
}
