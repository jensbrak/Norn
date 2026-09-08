using System.Reflection;

namespace Norn.UI;

/// <summary>
/// Norn's sole seam onto its own identity/version — every other file that
/// needs the app name, version, or copyright (the window title, the About
/// window) reads it from here, never by re-deriving from
/// <see cref="Assembly"/> or hardcoding a literal a second time. The actual
/// values are single-sourced one level further up, in <c>Directory.Build.props</c>
/// (<c>Product</c>/<c>Version</c>/<c>Copyright</c>), which the SDK turns into
/// this assembly's generated metadata attributes at build time — editing that
/// one file is enough for every reader of this class to pick up the change,
/// with no reflection or attribute lookup duplicated elsewhere.
/// </summary>
internal static class AppInfo
{
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    public static string Name { get; } =
        Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Norn";

    /// <summary>The full <c>Version</c> string as typed in
    /// <c>Directory.Build.props</c> (e.g. "0.1.0") — <see cref="AssemblyInformationalVersionAttribute"/>
    /// rather than <see cref="Assembly.GetName"/>'s four-part <see cref="System.Version"/>,
    /// since the latter pads with trailing zeros the source value never had.</summary>
    public static string Version { get; } =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static string Copyright { get; } =
        Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    /// <summary>Not MSBuild-derived — no SDK property maps cleanly to a
    /// project homepage the way Product/Version/Copyright do, so this is the
    /// single hand-maintained value instead.</summary>
    public const string HomepageUrl = "https://github.com/jensbrak/Norn";
}
