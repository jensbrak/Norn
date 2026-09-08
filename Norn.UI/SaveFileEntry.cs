using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// One discovered save file, independent of sort/filter/selection state.
/// Built per file at startup and recomputed only where something actually
/// changed it, rather than on every sidebar refresh.
/// <para>
/// This used to claim <see cref="LastModified"/> "doesn't change while Norn
/// runs" — which was never true of the file Norn itself just saved, and the
/// entry was only refreshed after a rename, so an ordinary save left the
/// sidebar tooltip and the modified-time sort showing startup values
/// (found in review). <c>MainWindow</c> now replaces the entry after every
/// successful save.
/// </para>
/// </summary>
public sealed record SaveFileEntry(string Path, string FileName, DateTime LastModified, bool IsBackup)
{
    public static SaveFileEntry From(string path)
    {
        // Fully qualified: the record's own Path parameter shadows the
        // System.IO.Path static class within this type's scope.
        var fileName = System.IO.Path.GetFileName(path);
        return new SaveFileEntry(
            path,
            System.IO.Path.GetFileNameWithoutExtension(path),
            File.GetLastWriteTime(path),
            SaveFileClassifier.IsBackupFile(fileName));
    }
}
