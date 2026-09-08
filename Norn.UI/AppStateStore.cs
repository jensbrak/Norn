namespace Norn.UI;

/// <summary>
/// Persists <see cref="AppState"/> as JSON at a fixed per-user path,
/// auto-saving on every change — same shape as <see cref="SettingsStore"/>,
/// just for silent app-tracked state instead of user preferences; the
/// shared load/save mechanics live in <see cref="JsonFileStore{T}"/>.
/// Replaces the earlier single-purpose <c>WelcomeStore</c>/<c>version.txt</c>
/// (no migration written for the old file — pre-1.0, single-developer
/// install, the only consequence of the old value going unread is the
/// welcome popup showing once more for a version already seen, which is
/// harmless).
/// </summary>
public static class AppStateStore
{
    private static readonly JsonFileStore<AppState> Store = new();

    public static AppState Current => Store.Current;

    /// <summary>Loads from <paramref name="path"/> if it exists and parses;
    /// otherwise leaves <see cref="Current"/> at its all-defaults value —
    /// the same graceful-degradation posture every other external-file seam
    /// in this app already takes.</summary>
    public static void Load(string path) => Store.Load(path);

    /// <summary>Writes <see cref="Current"/> back to the path <see cref="Load"/>
    /// was called with. A no-op if <see cref="Load"/> was never called.</summary>
    public static void Save() => Store.Save();
}
