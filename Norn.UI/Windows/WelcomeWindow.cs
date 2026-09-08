using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The welcome/disclaimer modal — shown once per <see cref="WelcomePolicy"/>
/// (first launch, or a major/minor version bump), opt-out via
/// <see cref="Settings.ShowWelcomePopup"/>. Shows the same top-down logo
/// (<see cref="AppIcon.TopDown"/>) as <see cref="AboutWindow"/> — not its
/// <see cref="AppIcon.Perspective"/> easter egg, which stays confined to
/// that one window — but is a distinct window, not a variant of it: the two
/// serve different purposes and their content diverges over time (About is
/// static reference info; this one's body is meant to change per version
/// once release notes exist to drive it).
/// <para>
/// Two content zones, deliberately not one paragraph: <see cref="Warning"/>
/// is fixed and version-independent — the actual safety point this window
/// exists for, backups plus the tested-Valheim-version disclosure
/// (<see cref="GameCompatibility.TestedValheimVersion"/>, informational
/// only — it doesn't gate anything; <c>GameCore.Version</c>'s own envelope
/// check is the thing that actually decides whether a save opens).
/// <see cref="WhatsNewPlaceholder"/> is the part expected to become
/// version-specific content later (a future CI/build step — explicitly out
/// of scope for this pass); keeping it visually and
/// structurally separate from the warning means that future work can only
/// ever touch this zone, never the safety text.
/// </para>
/// </summary>
internal sealed class WelcomeWindow : Window
{
    private const string Warning =
        "Norn edits Valheim character save files directly. Always keep a backup before making "
        + "changes — Norn cannot undo a change once it's saved to disk.\n\n"
        + "Norn is an unofficial, fan-made tool and is not affiliated with Iron Gate AB.\n\n"
        + $"Tested with Valheim {GameCompatibility.TestedValheimVersion}. Newer versions may still "
        + "work, but haven't been specifically verified.";

    /// <summary>Shown when <see cref="ReleaseNotesStore.ForVersion"/> finds
    /// no <c>## </c> section matching <see cref="AppInfo.Version"/> in
    /// <c>ReleaseNotes.md</c> — every version ships through this path until
    /// someone writes its entry.</summary>
    private const string WhatsNewPlaceholder = "(Release notes for this version aren't available yet.)";

    private WelcomeWindow()
    {
        Title = $"Welcome to {AppInfo.Name}";
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
            Text = $"Welcome to {AppInfo.Name} {AppInfo.Version}",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = Warning,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            Margin = new Thickness(0, 8, 0, 0),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "What's new",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(BuildWhatsNewBody());

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

    /// <summary>Not shared code with <see cref="AboutWindow.BuildLogo"/> —
    /// similar shape, but the two source different bitmaps
    /// (<see cref="AppIcon.TopDown"/> here vs. <see cref="AppIcon.Perspective"/>
    /// there) at different sizes, and there's nothing else here worth
    /// coupling them over one <c>Image</c>. <see cref="BitmapInterpolationMode.HighQuality"/>
    /// isn't optional polish — Avalonia's own default is <c>LowQuality</c>,
    /// which reads as visibly jagged for detailed source art scaled down
    /// this far.</summary>
    private static Control BuildLogo()
    {
        var image = new Image
        {
            Source = AppIcon.TopDown,
            Width = 128,
            Height = 128,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        return image;
    }

    /// <summary>The running version's release-notes section if
    /// <c>ReleaseNotes.md</c> has one, otherwise <see cref="WhatsNewPlaceholder"/>
    /// styled the way this zone always has been (italic, gray, centered) —
    /// real content, once it exists, reads as plain left-aligned prose/bullets
    /// instead, the same rendering <see cref="HelpWindow.BuildBlock"/> uses.
    /// Not shared code with it despite that: same reasoning as
    /// <see cref="BuildLogo"/> not being shared with <see cref="AboutWindow.BuildLogo"/> —
    /// short, and this zone's <c>MaxWidth</c>/centering context is its own.</summary>
    private static Control BuildWhatsNewBody()
    {
        var section = ReleaseNotesStore.ForVersion(AppInfo.Version);
        if (section is null)
        {
            return new TextBlock
            {
                Text = WhatsNewPlaceholder,
                FontStyle = FontStyle.Italic,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 380,
                TextAlignment = TextAlignment.Center,
            };
        }

        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, MaxWidth = 380 };
        foreach (var block in section.Blocks)
        {
            panel.Children.Add(BuildBlock(block));
        }

        return panel;
    }

    private static Control BuildBlock(MarkdownBlock block) => block switch
    {
        MarkdownParagraph paragraph => new TextBlock { Text = paragraph.Text, TextWrapping = TextWrapping.Wrap },
        MarkdownBulletList bullets => BuildBulletList(bullets),
        _ => throw new NotSupportedException($"No renderer for {block.GetType()}."),
    };

    private static Control BuildBulletList(MarkdownBulletList bullets)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var item in bullets.Items)
        {
            panel.Children.Add(new TextBlock { Text = $"• {item}", TextWrapping = TextWrapping.Wrap });
        }

        return panel;
    }

    /// <summary>Shows the welcome window modally over <paramref name="owner"/>.</summary>
    internal static Task Open(Window owner) => new WelcomeWindow().ShowDialog(owner);
}
