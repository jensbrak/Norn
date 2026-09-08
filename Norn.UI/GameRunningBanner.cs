using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>
/// Dismissible, non-blocking warning shown when <see cref="GameProcessShim"/>
/// detects Valheim running — editing a save while the game has it loaded
/// risks the same class of problem (lock conflicts, or the game overwriting
/// Norn's edit on its own next save/exit) that motivated excluding cloud
/// saves entirely. Advisory, not a block: the
/// detection is heuristic and weakest on Linux (see
/// <see cref="GameProcessDetector"/>'s remarks), so a false positive must
/// never lock a user out of legitimate editing.
/// </summary>
internal sealed class GameRunningBanner : Border
{
    private const string Message = "Valheim appears to be running — editing now risks losing changes or file conflicts. Close Valheim first.";

    // Same dark-mode warning family as StatusBar's dirty chip, but more
    // saturated: this flags an actual risk of data loss, not just pending
    // edits, so it should read as the more urgent of the two (found in
    // manual QA — the original colors were unreadable, white-on-near-white).
    private static readonly SolidColorBrush BannerBackground = new(Color.FromRgb(0x4D, 0x38, 0x00));
    private static readonly SolidColorBrush BannerForeground = new(Color.FromRgb(0xFF, 0xD6, 0x66));

    private bool _wasRunning;
    private bool _dismissedForThisRun;

    public GameRunningBanner()
    {
        Background = BannerBackground;
        Padding = new Avalonia.Thickness(8, 4);
        IsVisible = false;

        var text = new TextBlock
        {
            Text = Message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = BannerForeground,
            FontWeight = FontWeight.Bold,
        };
        var dismiss = new Button { Content = "Dismiss" };
        dismiss.Click += (_, _) =>
        {
            _dismissedForThisRun = true;
            IsVisible = false;
        };

        var row = new DockPanel();
        DockPanel.SetDock(dismiss, Dock.Right);
        row.Children.Add(dismiss);
        row.Children.Add(text);
        Child = row;
    }

    /// <summary>
    /// Re-checks whether the game is running. Only re-shows the banner on a
    /// fresh true→false→true transition — once dismissed, it stays dismissed
    /// until the game actually stops, so it doesn't reappear on every window
    /// focus while the user has already acknowledged it.
    /// </summary>
    public void Recheck()
    {
        var running = GameProcessShim.IsGameRunning();
        if (running && !_wasRunning)
        {
            _dismissedForThisRun = false;
        }

        _wasRunning = running;
        IsVisible = running && !_dismissedForThisRun;
    }
}
