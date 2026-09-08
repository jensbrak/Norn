using System.Xml.Linq;

namespace Norn.Tests;

/// <summary>
/// Asserts the declared project reference graph matches ARCHITECTURE.md exactly.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ArchitectureTests"/> inspects compiled manifests, which only record
/// references a project actually <i>used</i>. That answers "does UI touch
/// GameCore?" but not "could it?". This suite answers the second question by
/// reading the <c>.csproj</c> files, which is what "confirm UI cannot see
/// GameCore" actually means.
/// </para>
/// <para>
/// It needs the source tree, so it skips when only the built output is present.
/// The manifest assertions cover that case and run everywhere.
/// </para>
/// </remarks>
public class ProjectReferenceGraphTests
{
    /// <summary>
    /// The layering, transcribed. Each project's <i>direct</i> project references,
    /// exactly — extra entries fail as loudly as missing ones. <c>Norn.Tests</c> is
    /// deliberately absent: it references everything by design.
    /// </summary>
    private static readonly (string Project, string[] References)[] Expected =
    [
        ("Norn.GameCore.Primitives", []),
        ("Norn.GameCore", ["Norn.GameCore.Primitives"]),
        ("Norn.Adapter", ["Norn.GameCore"]),
        ("Norn.UI", ["Norn.Adapter"]),
    ];

    [Fact]
    public void Project_references_match_the_declared_architecture()
    {
        var root = RequireRepositoryRoot();

        foreach (var (project, expected) in Expected)
        {
            var actual = DirectProjectReferences(root, project);

            Assert.True(
                expected.Order(StringComparer.Ordinal).SequenceEqual(actual),
                $"Layering violation: {project} should reference [{string.Join(", ", expected)}] "
                + $"but references [{string.Join(", ", actual)}].");
        }
    }

    /// <summary>
    /// No source file in <c>Norn.UI</c> names the game layer at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the honest form of "UI cannot see GameCore". It cannot be enforced
    /// through the build graph: SDK-style project references flow transitively to
    /// compilation, and both levers that cut that flow —
    /// <c>PrivateAssets="compile"</c> on Adapter's reference, or
    /// <c>DisableTransitiveProjectReferences</c> on UI — also drop
    /// <c>Norn.GameCore.dll</c> from UI's output and <c>deps.json</c>, so the app
    /// fails at runtime the first time Adapter touches a GameCore type. Both were
    /// measured directly rather than assumed.
    /// </para>
    /// <para>
    /// So the compiler will let a UI file reach past Adapter, and this is what
    /// stops it. It catches the fully-qualified form that a <c>using</c>-only
    /// check misses, and unlike the PowerShell hook it runs on every platform.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_UI_source_file_names_the_game_layer()
    {
        var root = RequireRepositoryRoot();
        var uiDirectory = Path.Combine(root, "Norn.UI");

        var offenders = Directory
            .EnumerateFiles(uiDirectory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsBuildOutput(root, path))
            .Where(path => File.ReadAllText(path).Contains("Norn.GameCore", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Layering violation: Norn.UI may reference Adapter only, but these files name the "
            + $"game layer: {string.Join(", ", offenders)}");
    }

    private static bool IsBuildOutput(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);

        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                         || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string RequireRepositoryRoot()
    {
        var root = TestPaths.RepositoryRoot;
        Assert.SkipWhen(root is null, "Source tree not present; project files cannot be inspected.");
        return root!;
    }

    /// <summary>
    /// The <c>Include</c> paths of a project's <c>ProjectReference</c> items,
    /// reduced to bare project names.
    /// </summary>
    private static IReadOnlyList<string> DirectProjectReferences(string root, string project)
    {
        var projectFile = Path.Combine(root, project, project + ".csproj");
        Assert.True(File.Exists(projectFile), $"Expected a project file at {projectFile}.");

        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            // MSBuild writes Windows separators; normalise so this parses on Linux too.
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
