using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>
/// A hairline border wrapped around every dialog window's root content —
/// the one visible edge standing between "distinguishable from its parent"
/// and "invisible against it" on window managers that don't decorate
/// override-redirect/popup-style windows (most Linux compositors; Windows'
/// DWM always draws one). <see cref="Window.SystemDecorations"/> border and
/// shadow is native OS/WM behavior none of Norn's dialogs override, and it
/// varies by WM even across Linux desktops, so Norn draws its own rather
/// than depending on it — same reasoning as the status bar living in
/// in-window content rather than OS chrome.
/// <para>
/// Translucent gray rather than a light/dark pair: its alpha blends against
/// whatever is behind it, so it reads as a faint dark line on a light
/// background and a faint light line on a dark one without tracking
/// <c>ActualThemeVariant</c>. No shadow — on Windows this hairline sits
/// directly beside DWM's own border/shadow already, and a self-drawn shadow
/// on top of that would double up rather than help.
/// </para>
/// </summary>
internal static class DialogChrome
{
    private static readonly IBrush BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80));

    /// <summary>Wraps a dialog's root content in the shared hairline border.
    /// Call as the last step before assigning it to <see cref="Window.Content"/>.</summary>
    public static Control Wrap(Control content) => new Border
    {
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(1),
        Child = content,
    };
}
