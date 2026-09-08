using System.Reflection;

namespace Norn.Tests;

/// <summary>
/// The real gate for the project-layering boundaries described in ARCHITECTURE.md.
/// </summary>
/// <remarks>
/// <para>
/// <c>check-boundaries.ps1</c> is PowerShell and runs on the Windows development
/// host only — fast local feedback, not the gate. These assertions
/// hold on every platform and in CI. When a boundary rule changes, both change.
/// </para>
/// <para>
/// <b>Known limit.</b> The C# compiler omits references that a project declares
/// but never uses from the emitted manifest, so a passing assertion here proves
/// the assembly <i>does not use</i> the forbidden dependency, not that it could
/// not. <see cref="ProjectReferenceGraphTests"/> closes that gap by asserting the
/// declared reference graph instead.
/// </para>
/// </remarks>
public class ArchitectureTests
{
    [Fact]
    public void Ui_does_not_reference_GameCore()
    {
        AssertDoesNotReference("Norn", "Norn.GameCore");
    }

    [Fact]
    public void Ui_does_not_reference_Primitives()
    {
        AssertDoesNotReference("Norn", "Norn.GameCore.Primitives");
    }

    [Fact]
    public void Primitives_does_not_reference_Avalonia()
    {
        AssertDoesNotReference("Norn.GameCore.Primitives", "Avalonia");
    }

    [Fact]
    public void GameCore_does_not_reference_Avalonia()
    {
        AssertDoesNotReference("Norn.GameCore", "Avalonia");
    }

    [Fact]
    public void Adapter_does_not_reference_Avalonia()
    {
        AssertDoesNotReference("Norn.Adapter", "Avalonia");
    }

    [Fact]
    public void Primitives_does_not_reference_Unity()
    {
        AssertDoesNotReference("Norn.GameCore.Primitives", "UnityEngine");
    }

    [Fact]
    public void GameCore_does_not_reference_Unity()
    {
        AssertDoesNotReference("Norn.GameCore", "UnityEngine");
    }

    [Fact]
    public void Primitives_references_nothing_else_in_the_solution()
    {
        var referenced = ReferencedAssemblyNames("Norn.GameCore.Primitives")
            .Where(name => name.StartsWith("Norn.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(referenced);
    }

    /// <summary>
    /// Asserts that <paramref name="assemblyName"/> references neither
    /// <paramref name="forbiddenName"/> itself nor anything beneath it, so a
    /// future <c>Avalonia.Controls</c> or <c>Norn.GameCore.Something</c> is caught
    /// as well as the exact name.
    /// </summary>
    private static void AssertDoesNotReference(string assemblyName, string forbiddenName)
    {
        var offenders = ReferencedAssemblyNames(assemblyName)
            .Where(name => name.Equals(forbiddenName, StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(forbiddenName + ".", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Layering violation: {assemblyName} must not reference {forbiddenName}, but its manifest lists: "
            + string.Join(", ", offenders));
    }

    private static IReadOnlyList<string> ReferencedAssemblyNames(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));

        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();
    }
}
