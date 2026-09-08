namespace Norn.Tests;

/// <summary>
/// Supplies one test case per world-corpus file, and skips cleanly when the
/// corpus is absent — mirrors <see cref="Corpus"/> exactly, sibling location.
/// </summary>
public static class WorldCorpus
{
    public static IEnumerable<object?[]> Files()
    {
        var files = TestPaths.WorldCorpusFiles();

        if (files.Count == 0)
        {
            yield return [null];
            yield break;
        }

        foreach (var file in files)
        {
            yield return [file];
        }
    }

    internal static string RequireFile(string? fileName)
    {
        Assert.SkipWhen(
            fileName is null,
            "World-file corpus not present at reference/world-files; it is gitignored by design.");

        return Path.Combine(TestPaths.WorldCorpusDirectory!, fileName!);
    }
}
