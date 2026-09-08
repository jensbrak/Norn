using System.Text.RegularExpressions;

namespace Norn.GameCore;

// note:    Not a mirrored Load/Save method, so this carries no provenance
//          header in the sense the mirrored methods carry one — nothing here decodes a
//          ZPackage. It is still game-defined behavioral knowledge (the same
//          category as SkillType/Biome), specifically the filename shapes
//          SaveSystem.cs produces for backups. SaveSystem's backup machinery
//          is shared, type-agnostic code, so the same four shapes apply to
//          world (.fwl) backups as to character (.fch) ones and the
//          extension is a parameter here rather than baked in, letting one
//          regex serve both — true of the legacy flat-file layout both
//          extensions still use; 1.0.7's chunked world format writes the
//          same stems as directories with no extension, which this file's
//          extension check rejects by construction. That case is handled
//          separately, against the game's own looser timestamp regex, by
//          Norn.Adapter/WorldFileClassifier.cs's IsBackupPath.
//          The four shapes, by producer:
//            - SaveSystem.CreateBackup / MoveToBackup: "<name>_backup_<ts>"
//              (version-migration or storage-relocation backup)
//            - SaveSystem.ConsiderBackup: "<name>_backup_auto-<ts>"
//              (rolling auto-backup, runs after every successful save by
//              default — the shape most likely to actually appear on a real
//              user's disk)
//            - the cloud-write-failure fallback: "<name>_backup_cloud-<ts>",
//              from PlayerProfile.SavePlayerToDisk for characters
//              (characters_local) — which is exactly why this type is
//              shared, extension-parameterized code rather than two copies:
//              ZNet.GetBackupPath was the world-side (worlds_local)
//              counterpart through 0.221.10, but has no remaining caller as
//              of 1.0.7, so this shape is character-only there.
//            - SaveSystem.RestoreBackup, which does two separate things, one
//              per remaining shape: it first renames whatever file currently
//              sits at the primary location to "<name>_backup_restore-<ts>"
//              (displacing it to make room for the restore — this is the
//              restore- shape), then, only when the backup being restored is
//              itself SaveFileType.Single, copies that backup on to
//              "<name>_backup_<ts>" as well (a second producer of the plain
//              shape above, alongside CreateBackup/MoveToBackup) — for any
//              other backup type this second step targets the plain
//              "<name>" instead, emitting no backup-shaped file at all.
//          <ts> was NOT one consistent shape across the versions checked so
//          far, which is why both are matched rather than just the current
//          one — a backup file created before an upgrade doesn't get
//          rewritten by it, so both can be sitting on the same disk:
//            - 0.221.4 / 0.221.10 (identical between the two): every
//              producer above except ConsiderBackup literally called
//              DateTime.ToString("yyyyMMdd-HHmmss") (hyphenated, 8+6
//              digits); ConsiderBackup's auto-backup was the outlier,
//              calling ToString("yyyyMMddHHmmss") instead — no hyphen, 14
//              digits straight. Two different literals in the game's own
//              source, not a typo on this side.
//            - 1.0.7 (the earliest version checked in which this no longer
//              holds — nothing between 0.221.10 and 1.0.7 has been checked,
//              so it's confirmed changed at or before 1.0.7, not
//              necessarily changed by it): unified to one shared constant,
//              s_defaultDateFormat = "yyyyMMdd-HHmmss", used by every
//              producer including ConsiderBackup — the auto-backup outlier
//              above is gone.
//          All four literals (backup, auto, cloud, restore) are emitted
//          lowercase in every source path that produces them; the extension
//          is re-attached from the source file by Copy/Rename, not baked into
//          the naming expression, so it is checked separately here rather
//          than folded into the regex.
//          Deliberately NOT replicating the game's own looser classifier
//          (SaveSystem.GetSaveInfo, which accepts anything whose last 14
//          non-hyphen characters parse as a valid DateTime, regardless of the
//          literal "backup" marker) — requiring the literal marker is a
//          strictly safer, lower-false-positive check for Norn's purposes.
//          Digit counts are matched, not date validity: DateTime.ToString is
//          called with no explicit CultureInfo in source, so treating the
//          digits as an opaque count avoids a culture-dependent false
//          negative. Two more types the game's own classifier recognizes
//          that this deliberately doesn't match: SaveFileType.Rolling (an
//          underscore + trailing timestamp with no "_backup_" marker) — no
//          source path was found that produces it for characters — and
//          SaveFileType.OldBackup (a plain ".old" suffix), which Norn already
//          treats as a distinct, intentional concept of its own (the
//          .fch.old rotation) rather than folding it into "backup."

/// <summary>
/// Recognizes the filename shapes Valheim's own <c>SaveSystem</c> produces for
/// automatic backups — of character saves and, since <c>SaveSystem</c> is
/// shared machinery, world saves too — so callers can filter them out of a
/// file listing by default. Does not recognize manually renamed/copied files
/// (e.g. a user's own "CharacterOld.fch") — there is no reliable way to
/// distinguish those from an intentionally different name without guessing.
/// </summary>
public static class SaveFileBackupNaming
{
    private const string CharacterExtension = ".fch";

    private static readonly Regex BackupStemPattern = new(
        @"^.+_backup_(?:auto-\d{14}|(?:auto-|cloud-|restore-)?\d{8}-\d{6})$",
        RegexOptions.Compiled);

    /// <summary>
    /// True if <paramref name="fileName"/> is a <c>.fch</c> matching one of
    /// the game's own backup-naming shapes. Shorthand for
    /// <see cref="IsGameGeneratedBackupFileName(string, string)"/> with
    /// <c>".fch"</c>.
    /// </summary>
    public static bool IsGameGeneratedBackupFileName(string fileName)
        => IsGameGeneratedBackupFileName(fileName, CharacterExtension);

    /// <summary>
    /// True if <paramref name="fileName"/> has <paramref name="extension"/>
    /// and matches one of the game's own backup-naming shapes. The extension
    /// is checked case-insensitively; the "backup"/"auto"/
    /// "cloud"/"restore" markers are checked case-sensitively, since the game
    /// only ever emits them lowercase.
    /// </summary>
    public static bool IsGameGeneratedBackupFileName(string fileName, string extension)
    {
        if (!Path.GetExtension(fileName).Equals(extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        return BackupStemPattern.IsMatch(stem);
    }
}
