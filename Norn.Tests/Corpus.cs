namespace Norn.Tests;

/// <summary>
/// Supplies one test case per corpus file, and skips cleanly when the corpus is
/// absent.
/// </summary>
/// <remarks>
/// <para>
/// The corpus is gitignored, so a fresh clone has none of it.
/// That must skip, not fail. xUnit treats a <c>MemberData</c> source that yields
/// nothing as an error, so absence is signalled by yielding a single
/// <c>null</c> case that the test turns into a skip.
/// </para>
/// <para>
/// Cases carry the bare file name rather than the full path, so test names in
/// the report stay readable and stay free of a host-specific absolute path.
/// </para>
/// </remarks>
public static class Corpus
{
    public static IEnumerable<object?[]> Files()
    {
        var files = TestPaths.CorpusFiles();

        if (files.Count == 0)
        {
            yield return [null];
            yield break;
        }

        foreach (var file in files)
        {
            yield return [Path.GetFileName(file)];
        }
    }

    /// <summary>
    /// Resolves a case back to a full path, or skips the test when the case is
    /// the absent-corpus sentinel.
    /// </summary>
    internal static string RequireFile(string? fileName)
    {
        Assert.SkipWhen(
            fileName is null,
            "Save-file corpus not present at reference/save-files; it is gitignored by design.");

        return Path.Combine(TestPaths.CorpusDirectory!, fileName!);
    }
}
