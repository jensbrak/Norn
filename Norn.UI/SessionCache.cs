using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// Per-path cache of open editing sessions, so re-selecting an already-open
/// save doesn't re-hit disk and pending edits survive a sidebar round-trip.
/// </summary>
/// <remarks>
/// Exceptions from <see cref="CharacterEditor.Open"/> propagate — this class
/// doesn't swallow them. <c>MainWindow</c> decides how to show a failure.
/// </remarks>
internal sealed class SessionCache
{
    private readonly Dictionary<string, CharacterEditor> _sessions = new();

    /// <summary>Returns the cached session for <paramref name="path"/>, opening
    /// and caching it on first use. Returns <c>null</c> when the file is
    /// outside the compatible version range (never cached, since there's
    /// nothing to reuse) — <paramref name="incompatible"/> then says why, so
    /// the caller can show something more specific than a flat refusal.</summary>
    public CharacterEditor? GetOrOpen(string path, out IncompatibleVersion? incompatible)
    {
        if (_sessions.TryGetValue(path, out var existing))
        {
            incompatible = null;
            return existing;
        }

        var editor = CharacterEditor.Open(path, out incompatible);
        if (editor is not null)
        {
            _sessions[path] = editor;
        }

        return editor;
    }

    public bool TryGet(string path, out CharacterEditor? editor) => _sessions.TryGetValue(path, out editor);

    /// <summary>Moves a cached session from <paramref name="oldPath"/> to
    /// <paramref name="newPath"/> after <see cref="CharacterEditor.RenameFile"/>
    /// has already repointed the editor itself — keeps the dictionary key in
    /// sync with <see cref="CharacterEditor.Path"/> so a later lookup by the
    /// new path (e.g. re-selecting the sidebar row) still finds the session.</summary>
    public void Rekey(string oldPath, string newPath)
    {
        if (_sessions.Remove(oldPath, out var editor))
        {
            _sessions[newPath] = editor;
        }
    }
}
