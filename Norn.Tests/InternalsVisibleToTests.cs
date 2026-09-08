using System.Xml.Linq;

namespace Norn.Tests;

/// <summary>
/// Asserts <c>Norn.GameCore</c>'s <c>InternalsVisibleTo</c> grants exactly match
/// the deliberately-widened set, not by convention but by reading the
/// <c>.csproj</c> — extra entries fail as loudly as missing ones.
/// </summary>
/// <remarks>
/// The write path (<c>PlayerProfile.SavePlayerToDisk</c>) is <c>internal</c>
/// specifically so "no write path reachable from the UI" is a compile-time
/// fact. That fact depends entirely on this grant list staying exactly what
/// it's supposed to be — an accidental <c>Norn.UI</c> grant would silently
/// defeat it, and nothing else in the suite would notice.
/// </remarks>
public class InternalsVisibleToTests
{
    private static readonly string[] Expected = ["Norn.Adapter", "Norn.Tests"];

    [Fact]
    public void GameCore_grants_internals_visibility_to_exactly_Adapter_and_Tests()
    {
        var root = TestPaths.RepositoryRoot;
        Assert.SkipWhen(root is null, "Source tree not present; project file cannot be inspected.");

        var projectFile = Path.Combine(root!, "Norn.GameCore", "Norn.GameCore.csproj");
        Assert.True(File.Exists(projectFile), $"Expected a project file at {projectFile}.");

        var actual = XDocument.Load(projectFile)
            .Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            Expected.Order(StringComparer.Ordinal).SequenceEqual(actual),
            $"Norn.GameCore should grant InternalsVisibleTo to exactly "
            + $"[{string.Join(", ", Expected)}] but grants [{string.Join(", ", actual)}].");
    }
}
