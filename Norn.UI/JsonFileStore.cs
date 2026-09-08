using System.Text.Json;

namespace Norn.UI;

/// <summary>
/// The load/save mechanics <see cref="AppStateStore"/> and <see cref="SettingsStore"/>
/// share: JSON at a remembered per-user path, defaulting silently on a
/// missing or corrupt file rather than blocking the app, one auto-persisted
/// mutable instance. <c>AppState</c> and <c>Settings</c> stay two distinct,
/// separately named public stores — the "AppState vs. Settings" test in
/// ARCHITECTURE.md is a real conceptual line between forward-acting defaults
/// and passive continuity state, not one this class is meant to erase. It
/// exists only because the two stores' underlying plumbing was identical
/// down to the line and had drifted apart anyway: <c>SettingsStore.Save()</c>
/// went without <see cref="Save"/>'s try/catch for a while after
/// <c>AppStateStore.Save()</c> got one, purely because the fix
/// was applied to one copy and the duplicate went unnoticed. One shared
/// implementation means a precaution added for one store's plumbing is
/// added for both, by construction.
/// </summary>
internal sealed class JsonFileStore<T> where T : new()
{
    private string? _path;

    public T Current { get; private set; } = new();

    /// <summary>Loads from <paramref name="path"/> if it exists and parses;
    /// otherwise leaves <see cref="Current"/> at its all-defaults value.
    /// Remembers <paramref name="path"/> for <see cref="Save"/> regardless of
    /// whether a file existed to load.</summary>
    public void Load(string path)
    {
        _path = path;

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            if (loaded is not null)
            {
                Current = loaded;
            }
        }
        catch
        {
            // Corrupt/unparseable file: fall through to defaults rather
            // than blocking the app over it.
        }
    }

    /// <summary>Writes <see cref="Current"/> back to the path <see cref="Load"/>
    /// was called with. A no-op if <see cref="Load"/> was never called.</summary>
    public void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Same graceful-degradation posture as Load(): this is a
            // silently-persisted file, not user-critical data — losing one
            // write (e.g. an unwritable directory) should never be fatal.
        }
    }
}
