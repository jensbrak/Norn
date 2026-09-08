namespace Norn.Tests;

/// <summary>
/// A scratch path for the round-trip writer, deleted when the test ends.
/// </summary>
/// <remarks>
/// The writer takes a path because the game's does. Round-trip tests write to a
/// throwaway file and compare bytes — never back over a corpus file, which is
/// the one irrecoverable mistake available to this harness.
/// </remarks>
internal sealed class TempFile : IDisposable
{
    private TempFile(string path)
    {
        Path = path;
    }

    internal string Path { get; }

    internal static TempFile Create(string extension = ".fch")
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "norn-tests");
        Directory.CreateDirectory(directory);

        return new TempFile(System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N") + extension));
    }

    /// <summary>
    /// A path guaranteed not to exist, for tests asserting "missing
    /// file/directory" behavior — never created, unlike <see cref="Create"/>.
    /// Previously constructed inline, identically, in 8 test files (found in
    /// review).
    /// </summary>
    internal static string NonExistentPath(string extension = "")
        => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "norn-tests", Guid.NewGuid().ToString("N") + extension);

    public void Dispose()
    {
        // The siblings CharacterEditor.Save creates alongside the file it
        // writes: ".old" is its backup rotation, ".tmp" its staging target
        // (normally moved into place, but left behind by an interrupted
        // write). Deleting only the primary path left those accumulating in
        // the scratch directory after every passing save test (found in
        // review — the ".old" sibling is itself new, added by this same
        // review round's staging fix).
        foreach (var path in new[] { Path, Path + ".old", Path + ".tmp" })
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover scratch file is not worth failing a green test
                // over. UnauthorizedAccessException (a read-only fixture copy,
                // or a file still locked by an unflushed FileStream) used to be
                // uncaught here, so it could mask the actual test failure it
                // fired during teardown for (found in review).
            }
        }
    }
}
