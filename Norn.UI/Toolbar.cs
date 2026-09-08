using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>
/// The top-of-window button row: Save, Revert, Settings, Help, About, Exit.
/// Direct UI for actions that were previously reachable only via keyboard
/// shortcut or dialog buttons. Spans the full window width, not just the
/// tab area — these are
/// session-level actions, not per-tab ones. Settings/Help/About sit
/// immediately left of Exit, not grouped with Save/Revert — none of the
/// three is a session action at all, just anchored against the row's
/// nearest edge; each sits left of the next-oldest one, in the order they
/// were introduced.
/// </summary>
internal sealed class Toolbar : Border
{
    private readonly Button _save = new() { Content = "Save" };
    private readonly Button _revert = new() { Content = "Revert" };

    // Task-returning, not a plain Action: saving may show a modal
    // confirmation (the file-rename offer), so the click handler needs
    // something to await rather than firing-and-forgetting the save.
    public event Func<Task>? SaveRequested;

    // Task-returning for the same reason as SaveRequested: reverting can
    // fail (the file on disk no longer loads) and the handler reports that
    // through a modal, which the click handler needs to await rather than
    // fire and forget (found in review).
    public event Func<Task>? RevertRequested;
    public event Action? SettingsRequested;
    public event Action? HelpRequested;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public Toolbar()
    {
        Padding = new Avalonia.Thickness(8, 4);
        BorderThickness = new Avalonia.Thickness(0, 0, 0, 1);
        BorderBrush = Brushes.Gray;

        var settings = new Button { Content = "Settings" };
        var help = new Button { Content = "Help" };
        var about = new Button { Content = "About" };
        var exit = new Button { Content = "Exit" };

        // Awaits every subscriber in turn, not just SaveRequested() as a
        // single call: a multicast Func<Task> invoked that way only awaits
        // whichever handler sits last in the invocation list, letting any
        // other subscriber's save race ahead unawaited. Currently dormant
        // (MainWindow is the only subscriber) but fixed now rather than left
        // as a trap for the next handler added (found in review).
        _save.Click += async (_, _) =>
        {
            if (SaveRequested is null)
            {
                return;
            }

            foreach (var handler in SaveRequested.GetInvocationList().Cast<Func<Task>>())
            {
                await handler();
            }
        };
        _revert.Click += async (_, _) =>
        {
            if (RevertRequested is null)
            {
                return;
            }

            // Same per-subscriber await as SaveRequested above, same reason.
            foreach (var handler in RevertRequested.GetInvocationList().Cast<Func<Task>>())
            {
                await handler();
            }
        };
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        help.Click += (_, _) => HelpRequested?.Invoke();
        about.Click += (_, _) => AboutRequested?.Invoke();
        exit.Click += (_, _) => ExitRequested?.Invoke();

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(_save);
        row.Children.Add(_revert);
        row.Children.Add(settings);
        row.Children.Add(help);
        row.Children.Add(about);
        row.Children.Add(exit);
        Child = row;

        SetDirty(false);
    }

    /// <summary>Save/Revert are only meaningful when there are pending edits.</summary>
    public void SetDirty(bool dirty)
    {
        _save.IsEnabled = dirty;
        _revert.IsEnabled = dirty;
    }
}
