namespace Norn.UI;

/// <summary>
/// Persists <see cref="Settings"/> as JSON at a fixed per-user path,
/// auto-persisting on every change — settings are low-stakes compared to a
/// character save (no risk of losing hours of play from a wrong checkbox),
/// so unlike character editing they get none of the dirty-flag/explicit-
/// Save/Revert ceremony that actual game data gets:
/// every <see cref="SettingsWindow"/> control calls <see cref="Save"/> the
/// instant it changes. Same static load-once-with-an-explicit-reload-point
/// shape as <c>Norn.Adapter</c>'s <c>SharedItemDataCatalog</c>/
/// <c>WorldIdentityCatalog</c>, but holds one live mutable instance rather
/// than a lookup table — there's exactly one settings object, not many keyed
/// entries, so there's no <c>TryFind</c>-style API here. The shared
/// load/save mechanics (identical to <see cref="AppStateStore"/>'s) live in
/// <see cref="JsonFileStore{T}"/>.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonFileStore<Settings> Store = new();

    public static Settings Current => Store.Current;

    /// <summary>Loads from <paramref name="path"/> if it exists and parses;
    /// otherwise leaves <see cref="Current"/> at its all-defaults value — a
    /// missing or corrupt settings file degrades to defaults rather than
    /// blocking the app, the same graceful-degradation posture
    /// <c>SharedItemDataCatalog</c> takes toward its own external file.
    /// Remembers <paramref name="path"/> for <see cref="Save"/> regardless of
    /// whether a file existed to load.</summary>
    public static void Load(string path) => Store.Load(path);

    /// <summary>Writes <see cref="Current"/> back to the path <see cref="Load"/>
    /// was called with. A no-op if <see cref="Load"/> was never called.</summary>
    public static void Save() => Store.Save();
}
