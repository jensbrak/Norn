using System.Text.Json;

namespace Norn.Adapter;

/// <summary>
/// The loading rule shared by the JSON catalogs (<see cref="RecipeCatalog"/>,
/// <see cref="PieceCatalog"/>): the first candidate that exists and parses
/// wins; a missing or corrupt one is skipped, never fatal. Parsing is strict —
/// a missing or null required value rejects the whole file rather than
/// loading half of it.
/// </summary>
internal static class JsonCatalogFile
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>The first of <paramref name="candidatePaths"/> that exists and
    /// parses via <paramref name="parse"/>, or <c>null</c> when none does.</summary>
    internal static T? LoadFirst<T>(IEnumerable<string?> candidatePaths, Func<string, T> parse) where T : class
    {
        foreach (var path in candidatePaths)
        {
            if (path is null || !File.Exists(path))
            {
                continue;
            }

            try
            {
                return parse(File.ReadAllText(path));
            }
            catch
            {
                // Corrupt or unreadable candidate: try the next one.
            }
        }

        return null;
    }
}
