using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Norn.UI;

/// <summary>
/// Norn's sole seam onto its own logo art — every top-level <see cref="Window"/>
/// sets its <see cref="Window.Icon"/> to <see cref="Default"/> rather than
/// loading <c>Assets/norn.ico</c> itself, and <see cref="TopDown"/> /
/// <see cref="Perspective"/> are the two in-app logo bitmaps
/// (<see cref="WelcomeWindow"/> and <see cref="AboutWindow"/> respectively).
/// <see cref="Perspective"/> is a deliberate exception confined to
/// <see cref="AboutWindow"/> alone — every other surface in the app, including
/// <see cref="Default"/>, uses the top-down render.
/// <para>
/// The three source files (<c>Assets/norn.ico</c>, <c>norn-logo.png</c>,
/// <c>norn-logo-perspective.png</c>) are compiled in as Avalonia resources
/// (<c>avares://Norn/Assets/...</c> — the URI's assembly segment is
/// <see cref="AppInfo"/>'s underlying <c>AssemblyName</c>, "Norn", not the
/// <c>Norn.UI</c> project/root-namespace name the two otherwise share), so
/// they need no <c>AppContext.BaseDirectory</c> resolution and ship
/// regardless of how Norn is published.
/// </para>
/// </summary>
internal static class AppIcon
{
    public static WindowIcon Default { get; } = new(Open("norn.ico"));

    public static Bitmap TopDown { get; } = new(Open("norn-logo.png"));

    public static Bitmap Perspective { get; } = new(Open("norn-logo-perspective.png"));

    private static Stream Open(string assetFileName) =>
        AssetLoader.Open(new Uri($"avares://Norn/Assets/{assetFileName}"));
}
