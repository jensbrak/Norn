namespace Norn.UI;

/// <summary>
/// Pure "should the welcome popup show" decision — no I/O, no static state,
/// same split as <c>SaveDirectoryResolver</c>/<c>SaveDirectoryShim</c>: the
/// decision is a testable function, environment reading (the stored
/// last-seen version, the setting) lives in <see cref="AppStateStore"/>/
/// <see cref="Settings"/> instead. Triggers on a major or minor version
/// bump, or when <paramref name="lastSeenVersion"/> is <c>null</c> (never
/// shown before, including true first-ever launch) — the same check either
/// way, not two separate code paths for "first run" vs. "updated."
/// A patch-only bump (the trailing component) never triggers on its own.
/// </summary>
public static class WelcomePolicy
{
    public static bool ShouldShow(string? lastSeenVersion, string currentVersion, bool enabled)
    {
        if (!enabled)
        {
            return false;
        }

        if (lastSeenVersion is null)
        {
            return true;
        }

        // Malformed/unparseable version strings (either side) fail closed,
        // not open — silently never showing again is a smaller problem than
        // a startup crash over a display-only feature.
        if (!TryParseVersion(lastSeenVersion, out var last) || !TryParseVersion(currentVersion, out var current))
        {
            return false;
        }

        return current.Major > last.Major || (current.Major == last.Major && current.Minor > last.Minor);
    }

    /// <summary>
    /// <see cref="AppInfo.Version"/> may carry a SemVer-style pre-release
    /// suffix, which <see cref="Version.TryParse(string, out Version?)"/>
    /// cannot parse at all. It did when this was found — the shipping
    /// version was <c>1.0.0-beta</c> then, and every comparison above
    /// consequently failed closed on both sides, permanently disabling this
    /// feature from the very first launch onward rather than merely
    /// degrading for malformed input the way the comment above intends
    /// (found in review). The current version carries no suffix, but the
    /// guard stays: whether one is present is a property of whatever string
    /// ships, not something this policy should have to assume. Only the
    /// dotted numeric prefix matters for the major/minor comparison, so any
    /// suffix is stripped before parsing.
    /// </summary>
    private static bool TryParseVersion(string raw, out Version version)
    {
        var dashIndex = raw.IndexOf('-');
        var numeric = dashIndex >= 0 ? raw[..dashIndex] : raw;
        if (Version.TryParse(numeric, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }
}
