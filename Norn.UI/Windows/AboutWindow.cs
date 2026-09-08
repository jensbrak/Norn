using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// App identity modal: name, version, the tested Valheim version
/// (<see cref="GameCompatibility.TestedValheimVersion"/>, also shown in
/// <see cref="WelcomeWindow"/>), a logo, and a link to the project
/// homepage — reachable from <see cref="Toolbar"/>'s "About" button. Same
/// minimal fixed-content-modal shape as <see cref="MessageDialog"/>
/// (<see cref="Window.ShowDialog(Window)"/>, no XAML, <c>SizeToContent</c>),
/// not built on it directly since this window's content is fixed layout
/// (logo + several distinct lines), not one wrapped message string.
/// <c>*Window</c> vs. <c>*Dialog</c> naming (this file is the anchor
/// example for the former).
/// <para>
/// The logo uses <see cref="AppIcon.Perspective"/> — the one deliberate
/// exception to Norn using the top-down render (<see cref="AppIcon.TopDown"/>)
/// everywhere else, including <see cref="WelcomeWindow"/>'s own copy of this
/// same logo slot. A very deliberate easter egg, not an oversight.
/// </para>
/// </summary>
internal sealed class AboutWindow : Window
{
    private AboutWindow()
    {
        Title = $"About {AppInfo.Name}";
        Icon = AppIcon.Default;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        panel.Children.Add(BuildLogo());
        panel.Children.Add(new TextBlock
        {
            Text = AppInfo.Name,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Version {AppInfo.Version}",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        // Informational only, same as WelcomeWindow's own copy of this
        // fact — doesn't gate anything, just sets expectations (a newer
        // Valheim release may still open fine; GameCore.Version's own
        // envelope check is what actually decides that).
        panel.Children.Add(new TextBlock
        {
            Text = $"Tested with Valheim {GameCompatibility.TestedValheimVersion}",
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(BuildHomepageLink());
        panel.Children.Add(new TextBlock
        {
            Text = AppInfo.Copyright,
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        });
        // A nod, deliberately not an obligation. Norn shares no code with Loki
        // — it is a from-scratch project with a different architecture, and the
        // one control that had been ported from it was replaced with an
        // independent implementation — so nothing here is required by anyone's
        // license. Loki is simply where the idea of a Valheim character editor
        // came from, and it stays the reference point for several features.
        panel.Children.Add(new TextBlock
        {
            Text = "Thanks to Loki by Wufflez for inspiration and ideas",
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var ok = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
        };
        ok.Click += (_, _) => Close();
        panel.Children.Add(ok);

        Content = DialogChrome.Wrap(panel);
    }

    /// <summary>Not shared code with <see cref="WelcomeWindow.BuildLogo"/> —
    /// similar shape, but the two source different bitmaps
    /// (<see cref="AppIcon.Perspective"/> here vs. <see cref="AppIcon.TopDown"/>
    /// there) at different sizes, and there's nothing else here worth
    /// coupling them over one <c>Image</c>. Only <c>Width</c> is set —
    /// <c>Height</c> follows from <c>norn-logo-perspective.png</c>'s own
    /// (non-square, trimmed) aspect ratio under the default <c>Stretch.Uniform</c>,
    /// rather than a hardcoded square box that would letterbox it.
    /// <c>norn-logo-perspective.png</c> is a lossless crop of a native
    /// 256×256 direct render (242×173 after trim), not a resize of the
    /// 1024px master — deliberately, so the 220px display width here is a
    /// near-1:1 downscale rather than a resampled-then-resampled-again one
    /// <see cref="BitmapInterpolationMode.HighQuality"/>
    /// still matters even at this near-native ratio — Avalonia's own
    /// default is <c>LowQuality</c>.</summary>
    private static Control BuildLogo()
    {
        var image = new Image
        {
            Source = AppIcon.Perspective,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        return image;
    }

    /// <summary>Styled as a link (underline, hand cursor) rather than an
    /// ordinary <see cref="Button"/> — matches the affordance-via-cursor
    /// convention <see cref="Norn.UI.Tabs.InventoryTabModule"/>'s tile clicks already
    /// use, applied to a text link instead of a whole tile.</summary>
    private static Control BuildHomepageLink()
    {
        var link = new TextBlock
        {
            Text = AppInfo.HomepageUrl,
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3a, 0x6b, 0xc4)),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        link.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(link).Properties.IsLeftButtonPressed)
            {
                OpenHomepage();
            }
        };
        return link;
    }

    /// <summary>
    /// <c>UseShellExecute = true</c> with a URL as the file name resolves
    /// through the registered browser association on Windows, but has no
    /// such association to resolve on Linux — <c>xdg-open</c> is the
    /// platform's own equivalent there (platform-dependent
    /// behavior stays isolated to a small gated shim like this one, never
    /// assumed to work identically). Best-effort: a missing browser/xdg-open
    /// association is a plausible environment gap, not a Norn bug, so a
    /// failure here is swallowed rather than crashing the app over a modal
    /// info window's link.
    /// </summary>
    private static void OpenHomepage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(AppInfo.HomepageUrl) { UseShellExecute = true });
            }
            else
            {
                Process.Start("xdg-open", AppInfo.HomepageUrl);
            }
        }
        catch
        {
            // No browser association / no xdg-open on PATH: nothing this
            // window can do about it, and nothing worth surfacing a dialog
            // over for a single info-window link.
        }
    }

    /// <summary>Shows the About window modally over <paramref name="owner"/>.
    /// Named <c>Open</c>, not <c>Show</c> — matching <see cref="WorldMapWindow.Open"/>'s
    /// naming, and for the same reason: a static <c>Show(Window)</c> here
    /// would hide the inherited instance <see cref="Window.Show(Window)"/>.</summary>
    internal static Task Open(Window owner) => new AboutWindow().ShowDialog(owner);
}
