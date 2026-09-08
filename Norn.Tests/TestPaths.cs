namespace Norn.Tests;

/// <summary>
/// Resolves repo-relative paths at test time.
/// </summary>
/// <remarks>
/// <para>
/// Asserting a literal absolute path would bind the suite to one machine, and the corpus sits
/// outside the test binary's output directory. Both are solved the same way:
/// walk up from the test assembly until the solution file appears, then compose
/// everything below with <see cref="Path.Combine"/>.
/// </para>
/// <para>
/// Corpus enumeration compares the extension explicitly rather than globbing
/// <c>*.fch</c>. A glob is case-insensitive on Windows and case-sensitive on
/// Linux, so a <c>.FCH</c> file would be picked up on the development host and
/// silently dropped for a Linux user — exactly the class of bug that passes
/// on a developer's machine and fails for a user.
/// </para>
/// </remarks>
internal static class TestPaths
{
    private const string SolutionFileName = "Norn.slnx";
    private const string SaveFileExtension = ".fch";
    private const string WorldFileExtension = ".fwl";

    /// <summary>Repository root, or <c>null</c> when the source tree is absent.</summary>
    internal static string? RepositoryRoot { get; } = FindRepositoryRoot(AppContext.BaseDirectory);

    /// <summary>
    /// The save-file corpus directory, or <c>null</c> when it is absent. The corpus
    /// is gitignored, so absence is normal and must be skipped
    /// cleanly rather than failed.
    /// </summary>
    internal static string? CorpusDirectory
    {
        get
        {
            if (RepositoryRoot is null) return null;
            var path = Path.Combine(RepositoryRoot, "reference", "save-files");
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// The shared item-data CSV — an ordinary tracked resource under
    /// <c>Norn.UI/Content/</c>, unlike the corpus. Still
    /// nullable/skip-safe (<c>RepositoryRoot</c> itself can be absent, e.g.
    /// running from a standalone publish output with no source tree
    /// alongside it), but no longer expected to be missing in an ordinary
    /// checkout.
    /// </summary>
    internal static string? SharedItemDataCsvPath
    {
        get
        {
            if (RepositoryRoot is null) return null;
            var path = Path.Combine(RepositoryRoot, "Norn.UI", "Content", "SharedItemData.csv");
            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// The localization-string export — an ordinary tracked resource
    /// under <c>Norn.UI/Content/</c>, same treatment as <see cref="SharedItemDataCsvPath"/>.
    /// </summary>
    internal static string? LocalizationDataCsvPath
    {
        get
        {
            if (RepositoryRoot is null) return null;
            var path = Path.Combine(RepositoryRoot, "Norn.UI", "Content", "LocalizationData.csv");
            return File.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// Every <c>.fch</c> file in the corpus, ordered so test output is stable.
    /// Empty when the corpus is absent.
    /// </summary>
    internal static IReadOnlyList<string> CorpusFiles()
    {
        var directory = CorpusDirectory;
        if (directory is null) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).Equals(SaveFileExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The curated fixture directory — a small, tracked subset of real
    /// Valheim-written saves, chosen to jointly cover every distinct profile
    /// version the real corpus has, so R1/R2 still run against something
    /// real in a checkout with no gitignored corpus at all. Unlike
    /// <see cref="CorpusDirectory"/> this is an ordinary tracked resource, same
    /// treatment as <see cref="SharedItemDataCsvPath"/> — not expected to be
    /// missing in an ordinary checkout, but still nullable for the same
    /// standalone-publish-output edge case.
    /// </summary>
    internal static string? FixtureDirectory
    {
        get
        {
            if (RepositoryRoot is null) return null;
            var path = Path.Combine(RepositoryRoot, "Norn.Tests", "Fixtures");
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// Every <c>.fch</c> fixture, ordered so test output is stable. Unlike
    /// <see cref="CorpusFiles"/> this is never expected to be empty.
    /// </summary>
    internal static IReadOnlyList<string> FixtureFiles()
    {
        var directory = FixtureDirectory;
        if (directory is null) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).Equals(SaveFileExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every <c>.fwl</c> fixture in the same tracked <see cref="FixtureDirectory"/>
    /// as <see cref="FixtureFiles"/> (e.g. <c>fixture-v43-fresh.fwl</c>) — this
    /// used to exist on disk but was never enumerated by any test, since
    /// <see cref="FixtureFiles"/> filters strictly for <c>.fch</c> (found in
    /// review: <see cref="WorldCorpusTests"/> only ever looks at the
    /// gitignored real corpus, so a fresh clone got zero real <c>.fwl</c>/
    /// <c>World.Load</c> coverage at all). Never expected to be empty, same
    /// as <see cref="FixtureFiles"/>.
    /// </summary>
    internal static IReadOnlyList<string> WorldFixtureFiles()
    {
        var directory = FixtureDirectory;
        if (directory is null) return [];

        return Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).Equals(WorldFileExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The world-file corpus directory,
    /// or <c>null</c> when absent — same gitignored-by-design treatment as
    /// <see cref="CorpusDirectory"/>, sibling location.
    /// </summary>
    internal static string? WorldCorpusDirectory
    {
        get
        {
            if (RepositoryRoot is null) return null;
            var path = Path.Combine(RepositoryRoot, "reference", "world-files");
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// Every world-metadata file in the corpus, as a path relative to the
    /// corpus root, ordered so test output is stable. Empty when the corpus is
    /// absent.
    /// </summary>
    /// <remarks>
    /// Goes through <see cref="Norn.UI.WorldFileLocator"/> rather than
    /// enumerating <c>.fwl</c> directly, so the corpus sees exactly what the
    /// app sees — including Valheim 1.0's directory-per-world layout, which a
    /// flat file enumeration misses entirely. Relative rather than bare file
    /// names because a chunked world's file is always <c>_main.&lt;N&gt;.fwl2</c>;
    /// bare names would collide across worlds and read meaninglessly in test
    /// output.
    /// </remarks>
    internal static IReadOnlyList<string> WorldCorpusFiles()
    {
        var directory = WorldCorpusDirectory;
        if (directory is null) return [];

        return Norn.UI.WorldFileLocator.FindWorldFiles([directory])
            .Select(path => Path.GetRelativePath(directory, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every world-metadata file physically present in the corpus, backups
    /// included, in both layouts — the raw ground truth
    /// <see cref="WorldCorpusFiles"/> is a filtered view of.
    /// </summary>
    /// <remarks>
    /// Exists so a test can assert what the locator <i>excludes</i>. Deriving
    /// that from <see cref="WorldCorpusFiles"/> would be circular: it is the
    /// locator's own output, so nothing it excluded is in it to begin with.
    /// </remarks>
    internal static IReadOnlyList<string> WorldCorpusFilesUnfiltered()
    {
        var directory = WorldCorpusDirectory;
        if (directory is null) return [];

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Norn.Adapter.WorldFileClassifier.IsWorldMetaFile(Path.GetFileName(path)))
            .Select(path => Path.GetRelativePath(directory, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
