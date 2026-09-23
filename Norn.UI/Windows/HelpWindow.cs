using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Norn.UI;

/// <summary>
/// The Help window: <see cref="HelpStore.Sections"/> rendered as one
/// continuous, scrollable page — a "Jump to a section" list at the top
/// (the <c>#section</c> anchor-link idea, applied without needing any real
/// HTML), then every section in order. <c>*Window</c>, not <c>*Dialog</c>
/// — this is a genuine content surface, not a
/// single question. Fixed initial size plus resizable, same shape as
/// <see cref="WorldMapWindow"/> (the one other content-heavy window in the
/// app) rather than the small fixed-size <c>SizeToContent</c> convention
/// every other modal here uses — prose specifically benefits from a reader
/// being able to make the window bigger.
/// <para>
/// Deliberately one scrollable document, not a <see cref="TabControl"/>:
/// tabs imply genuinely separate surfaces the way General/Vitals/Inventory
/// are; this content is short, purposeful sections meant to read like a
/// README (which — see <c>HelpContent.md</c>'s own closing section — it's
/// written to literally become, once Norn has a public repository), and a
/// README doesn't have tabs.
/// </para>
/// </summary>
internal sealed class HelpWindow : Window
{
    private readonly Dictionary<string, Control> _sectionAnchors = new();

    private HelpWindow(string? section)
    {
        Title = $"{AppInfo.Name} help";
        Icon = AppIcon.Default;
        Width = 640;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16, Margin = new Thickness(24) };
        content.Children.Add(BuildHeader());

        foreach (var helpSection in HelpStore.Sections)
        {
            var sectionControl = BuildSection(helpSection);
            _sectionAnchors[helpSection.Title] = sectionControl;
            content.Children.Add(sectionControl);
        }

        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();
        content.Children.Add(ok);

        Content = DialogChrome.Wrap(new ScrollViewer { Content = content });

        if (section is not null && _sectionAnchors.TryGetValue(section, out var target))
        {
            // Opened, not inline here: the target needs a real layout pass
            // (and this window needs to actually be shown) before scrolling
            // to it means anything — same reasoning MainWindow's own
            // Opened-deferred welcome-popup check already uses.
            Opened += (_, _) => target.BringIntoView();
        }
    }

    /// <summary>The table of contents plus <see cref="AppIcon.TopDown"/>,
    /// docked to the right and sized to match — not a fixed size, since
    /// the logo should grow with the ToC as
    /// <see cref="HelpStore.Sections"/> gains entries, not stay pinned at
    /// today's section count. Tracked via <see cref="Control.SizeChanged"/>
    /// rather than computed from font/line-height metrics by hand — this
    /// codebase already got burned once guessing at Avalonia's own text
    /// layout math (see <see cref="BuildBulletList"/>'s doc comment), so the
    /// actual rendered height is measured, not predicted. Square source
    /// (<see cref="Stretch.Uniform"/>, the <c>Image</c> default) means width
    /// grows right along with height — accepted as fine up to
    /// the point it would ever collide with the ToC text, which isn't
    /// guarded against here since it isn't reachable at any section count
    /// this app ships with today.</summary>
    private Control BuildHeader()
    {
        var toc = BuildTableOfContents();

        var logo = new Image
        {
            Source = AppIcon.TopDown,
            // Without an explicit starting Height, Image's first (unconstrained)
            // measure pass returns the source bitmap's full native pixel size
            // (norn-logo.png is 512x512) — DockPanel would then reserve that
            // much width for this Right-docked child before toc.SizeChanged
            // ever fires once, squeezing toc's own available width enough to
            // wrap its links, which inflates toc's height, which this control
            // then locks onto: a bad, stable-but-wrong state, not a genuine
            // infinite growth loop, but just as broken (found by running the
            // app — the logo filled almost the entire window). A tiny
            // placeholder here keeps that first pass from ever mattering.
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        RenderOptions.SetBitmapInterpolationMode(logo, BitmapInterpolationMode.HighQuality);
        toc.SizeChanged += (_, e) => logo.Height = e.NewSize.Height;

        var header = new DockPanel();
        DockPanel.SetDock(logo, Dock.Right);
        header.Children.Add(logo);
        header.Children.Add(toc);
        return header;
    }

    private Control BuildTableOfContents()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Jump to a section:", FontWeight = FontWeight.Bold });

        foreach (var helpSection in HelpStore.Sections)
        {
            panel.Children.Add(BuildSectionLink(helpSection.Title));
        }

        return panel;
    }

    /// <summary>Same underline/hand-cursor link styling as
    /// <see cref="AboutWindow.BuildHomepageLink"/>, applied to an in-window
    /// scroll target instead of an external URL.</summary>
    private Control BuildSectionLink(string title)
    {
        var link = new TextBlock
        {
            Text = title,
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.FromRgb(0x3a, 0x6b, 0xc4)),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        link.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(link).Properties.IsLeftButtonPressed && _sectionAnchors.TryGetValue(title, out var target))
            {
                target.BringIntoView();
            }
        };
        return link;
    }

    private static Control BuildSection(MarkdownSection section)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = section.Title, FontSize = 16, FontWeight = FontWeight.Bold });

        foreach (var block in section.Blocks)
        {
            panel.Children.Add(BuildBlock(block));
        }

        panel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 0) });

        return panel;
    }

    private static Control BuildBlock(MarkdownBlock block) => block switch
    {
        MarkdownParagraph paragraph => new TextBlock { Text = paragraph.Text, TextWrapping = TextWrapping.Wrap },
        MarkdownBulletList bullets => BuildBulletList(bullets),
        // MarkdownSectionParser only ever produces the two cases above —
        // this is a backstop for a MarkdownBlock subtype added without a
        // matching renderer, not the primary catch for the mistake.
        _ => throw new NotSupportedException($"No renderer for {block.GetType()}."),
    };

    /// <summary>
    /// One flat <c>"• item"</c> <see cref="TextBlock"/> per item — not a
    /// hanging indent (bullet glyph in its own column, wrapped continuation
    /// lines aligned under the item text, not the glyph), despite that
    /// being tried first. Four variations were tested live (a Grid per
    /// item; the same shape as <see cref="RowGroup"/>'s own working
    /// label/data/button/description grid, whose description column does
    /// wrap correctly on Vitals' "Time since death" row; explicit
    /// <c>HorizontalAlignment.Stretch</c>; explicit <c>VerticalAlignment.Top</c>)
    /// and all four wrapped continuation lines flush left instead of
    /// hanging under the item text — a genuine, reproduced Avalonia
    /// rendering quirk this didn't track down given placeholder content
    /// isn't worth unbounded debugging time on. Revisit if this stops being
    /// placeholder content and the flat wrap still reads poorly.
    /// </summary>
    private static Control BuildBulletList(MarkdownBulletList bullets)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var item in bullets.Items)
        {
            panel.Children.Add(new TextBlock { Text = $"• {item}", TextWrapping = TextWrapping.Wrap });
        }

        return panel;
    }

    /// <summary>Shows the Help window modally over <paramref name="owner"/>,
    /// scrolled to <paramref name="section"/> if given and found — a
    /// missing/unrecognized section name just opens at the top, not an
    /// error, since this is called from in-app UI a future contextual "?"
    /// link would drive, not user-typed input.</summary>
    internal static Task Open(Window owner, string? section = null) => new HelpWindow(section).ShowDialog(owner);
}
