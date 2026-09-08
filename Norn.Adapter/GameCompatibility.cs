namespace Norn.Adapter;

/// <summary>
/// Game-derived facts that don't belong in <c>GameCore</c>'s wire-format
/// mirror — just the human-readable Valheim release this
/// Norn build was last verified against. Purely informational: it makes no
/// promise about whether a save from a newer release will open. That's
/// decided entirely by <c>GameCore.Version</c>'s own machine-readable
/// envelope-version gate (<c>IsPlayerVersionCompatible</c>/
/// <c>IsWorldVersionCompatible</c>), which doesn't move on every Valheim
/// patch — a release genuinely newer than <see cref="TestedValheimVersion"/>
/// can still open fine here if the wire format didn't change, or get
/// refused by that separate gate if it did. This constant only states what
/// was actually checked, not what will or won't work.
/// </summary>
public static class GameCompatibility
{
    // game-derived: the Valheim version confirmed against during the last
    // full patch-day pass. Bump by hand as part of that same pass, not
    // independently of it.
    public const string TestedValheimVersion = "1.0.7";
}
