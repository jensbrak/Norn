using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Norn.UI;

/// <summary>
/// The bottom status area: path and dirty state for whatever's currently
/// shown. Lives in the window's own drawn content, not the OS title bar —
/// title-bar chrome renders inconsistently across Linux desktop environments,
/// so anything that must always be visible belongs here instead.
/// </summary>
internal sealed class StatusBar : Border
{
    private const string Ellipsis = "…";
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(3);

    private readonly TextBlock _pathText = new();
    // Dark-mode warning family, not the default (light) text color over a
    // pale background — that combination was unreadable (white-on-near-white)
    // until this was caught in manual QA. Kept deliberately less saturated
    // than GameRunningBanner's colors: this is "you have pending edits," not
    // "something risky is happening".
    private static readonly SolidColorBrush DirtyChipBackground = new(Color.FromRgb(0x33, 0x2B, 0x00));
    private static readonly SolidColorBrush DirtyChipForeground = new(Color.FromRgb(0xE3, 0xB3, 0x41));

    private readonly TextBlock _dirtyLabel = new()
    {
        Text = "Unsaved changes",
        FontWeight = FontWeight.Bold,
        Foreground = DirtyChipForeground,
    };

    private readonly Border _dirtyChip;

    // Plain text, no chip border — deliberately a lighter visual weight than
    // the dirty chip: that's persistent state ("this file has pending
    // edits"), this is a fleeting note about an action just taken, and
    // giving it the same boxed treatment would blend the two together
    // (the tile-interaction-model pass this was built for flagged that risk
    // directly). A cool, distinct hue from the dirty chip's warm
    // amber keeps them from reading as the same kind of signal even in
    // peripheral vision.
    private static readonly SolidColorBrush MessageForeground = new(Color.FromRgb(0x5D, 0xB8, 0x8C));

    private readonly TextBlock _messageLabel = new()
    {
        Foreground = MessageForeground,
        Margin = new Avalonia.Thickness(0, 0, 12, 0),
        IsVisible = false,
    };

    private readonly DispatcherTimer _messageTimer;

    private string? _fullPath;

    public StatusBar()
    {
        Padding = new Avalonia.Thickness(8, 4);
        BorderThickness = new Avalonia.Thickness(0, 1, 0, 0);
        BorderBrush = Brushes.Gray;

        _dirtyChip = new Border
        {
            Padding = new Avalonia.Thickness(6, 1),
            Background = DirtyChipBackground,
            Child = _dirtyLabel,
            IsVisible = false,
        };

        _messageTimer = new DispatcherTimer { Interval = MessageDuration };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            _messageLabel.IsVisible = false;
        };

        // DockPanel, not the previous horizontal StackPanel: the dirty chip
        // is docked to the right edge and the path text fills whatever
        // remains, so the chip stays pinned to the window edge regardless of
        // path length instead of just trailing the path text. The message
        // label docks right too, added between the chip and the path text so
        // it lands immediately to the chip's left — closer to "Unsaved
        // changes" than to the path.
        var row = new DockPanel();
        DockPanel.SetDock(_dirtyChip, Dock.Right);
        DockPanel.SetDock(_messageLabel, Dock.Right);
        row.Children.Add(_dirtyChip);
        row.Children.Add(_messageLabel);
        row.Children.Add(_pathText);
        Child = row;

        // _pathText's own SizeChanged, not the StatusBar's: it fires exactly
        // when the box DockPanel computed for the path text changes size —
        // whether from a window resize or the dirty chip appearing/
        // disappearing — which is precisely the available width the
        // compaction below needs.
        _pathText.SizeChanged += (_, _) => UpdatePathDisplay();

        Show(null, false);
    }

    /// <summary>Updates the display. <paramref name="path"/> null means
    /// nothing is currently loaded.</summary>
    public void Show(string? path, bool dirty)
    {
        _fullPath = path;
        _dirtyChip.IsVisible = dirty;
        UpdatePathDisplay();
    }

    /// <summary>
    /// Posts a transient action-feedback note (e.g. "Repaired 3 items"),
    /// replacing whatever's currently showing and restarting the timer — no
    /// queue. Deliberately simple for a first pass: a queue only pays for
    /// itself once messages can hand off with some animation, which Norn has
    /// nowhere else yet; without that, a queue would just delay news the
    /// user wants now behind older news.
    /// </summary>
    public void ShowMessage(string text)
    {
        _messageLabel.Text = text;
        _messageLabel.IsVisible = true;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    private void UpdatePathDisplay()
    {
        _pathText.Text = _fullPath is null ? "No file open" : Compact(_fullPath, _pathText.Bounds.Width);
    }

    /// <summary>
    /// When <paramref name="path"/> is too wide for <paramref name="availableWidth"/>,
    /// replaces its left portion with an ellipsis and keeps the tail — the
    /// informative part, filename and nearest containing folder — visible,
    /// e.g. "…\characters_local\PlayerName.fch". Text stays left-aligned
    /// always; only the string content changes (chosen over flipping
    /// alignment left/right on overflow, which would jump as the window
    /// resizes across the threshold).
    /// </summary>
    private string Compact(string path, double availableWidth)
    {
        if (availableWidth <= 0 || TextMeasurement.Width(_pathText, path) <= availableWidth)
        {
            return path;
        }

        var lo = 0;
        var hi = path.Length;
        var best = Ellipsis;

        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var candidate = Ellipsis + path[^mid..];
            if (TextMeasurement.Width(_pathText, candidate) <= availableWidth)
            {
                best = candidate;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return best;
    }
}
