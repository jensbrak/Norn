using Norn.GameCore;

namespace Norn.Adapter;

/// <summary>
/// The only legal path from <c>Norn.UI</c> to backup-filename knowledge:
/// <c>Norn.UI</c> cannot reference <c>Norn.GameCore</c> directly, so this
/// forwards to <see cref="SaveFileBackupNaming"/>. Also the
/// named seam for a future Norn-created backup convention — none exists
/// today, since <c>CharacterEditor.Save()</c>'s <c>.old</c> file is already
/// excluded from any save-file listing by extension alone.
/// </summary>
public static class SaveFileClassifier
{
    public static bool IsBackupFile(string fileName) => SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName);
}
