namespace Norn.UI;

/// <summary>
/// Everything Norn silently remembers about how it was last used — not a
/// user preference (that's <see cref="Settings"/>, which generates a UI
/// row from every property), just app-tracked bookkeeping a user never
/// sees or edits directly. One consolidated class/file, for the same
/// reason <see cref="Settings"/> is one class/file rather than one per
/// feature: "is this app state" is the same kind of question regardless of
/// which feature it's about, so it gets one home from the start rather
/// than a new small file every time a new piece of state shows up
/// (prompted by realizing the welcome popup's
/// last-seen-version tracking was already this exact category of fact,
/// just not named as such yet). A plain POCO, no attribute/reflection
/// machinery the way <see cref="Settings"/> has — there's no generated UI
/// to reflect for, since nothing here is ever shown as a row to configure.
/// <para>
/// Deciding whether a new field belongs here or in <see cref="Settings"/>:
/// see ARCHITECTURE.md's "AppState vs. Settings" test
/// (forward-acting behavioral default vs. passive view/continuity state) —
/// worth reading in full before adding one, since "does this persist" alone
/// doesn't distinguish them.
/// </para>
/// </summary>
public sealed class AppState
{
    /// <summary>The last app version the welcome popup was shown for —
    /// <c>null</c> means never, including true first-ever launch. See
    /// <see cref="WelcomePolicy.ShouldShow"/>.</summary>
    public string? LastSeenVersion { get; set; }

    /// <summary>Defaults match the sidebar's own pre-persistence behavior
    /// exactly, so a missing state file (a fresh install, or this field
    /// simply not existing yet in an older state file) looks identical to
    /// today.</summary>
    public SaveFileSortKey SidebarSortKey { get; set; } = SaveFileSortKey.Modified;

    public bool SidebarSortDescending { get; set; } = true;

    /// <summary>Last state of the World Map window's "Show explored by
    /// others" checkbox. Defaults to <c>false</c>, matching that checkbox's
    /// own pre-persistence default (own pins only).</summary>
    public bool WorldMapShowExploredByOthers { get; set; }

    /// <summary>Last state of the World Map window's "Show points"
    /// checkbox. Defaults to <c>true</c>, matching that checkbox's own
    /// pre-persistence default.</summary>
    public bool WorldMapShowPoints { get; set; } = true;
}
