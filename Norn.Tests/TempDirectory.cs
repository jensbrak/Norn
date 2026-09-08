namespace Norn.Tests;

/// <summary>A scratch directory for filesystem-touching tests, deleted when the test ends.</summary>
internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path)
    {
        Path = path;
    }

    internal string Path { get; }

    internal static TempDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "norn-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return new TempDirectory(path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover scratch directory is not worth failing a green test
            // over. UnauthorizedAccessException (a read-only/locked entry)
            // used to be uncaught here, so it could mask the actual test
            // failure it fired during teardown for (found in review).
        }
    }
}
