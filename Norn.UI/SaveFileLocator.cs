namespace Norn.UI;

/// <summary>
/// Finds save files across one or more directories. The extension is compared
/// explicitly rather than globbed: a <c>*.fch</c> glob is case-insensitive on
/// Windows and case-sensitive on Linux, so a <c>.FCH</c> file would be picked
/// up on the development host and silently dropped for a Linux user.
/// </summary>
public static class SaveFileLocator
{
    private const string SaveFileExtension = ".fch";

    public static IReadOnlyList<string> FindSaveFiles(IEnumerable<string> directories)
    {
        var files = new List<string>();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            files.AddRange(Directory.EnumerateFiles(directory)
                .Where(path => Path.GetExtension(path).Equals(SaveFileExtension, StringComparison.OrdinalIgnoreCase)));
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToList();
    }
}
