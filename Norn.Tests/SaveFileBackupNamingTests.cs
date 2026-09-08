using Norn.GameCore;

namespace Norn.Tests;

/// <summary>
/// Exercises <see cref="SaveFileBackupNaming"/> against all four confirmed
/// game-generated backup shapes, plus names it must NOT match — most
/// importantly manually renamed files, which are explicitly out of scope.
/// </summary>
public class SaveFileBackupNamingTests
{
    [Theory]
    [InlineData("alice_backup_20260810-143005.fch")]
    [InlineData("alice_backup_auto-20260810143005.fch")]
    [InlineData("alice_backup_cloud-20260810-143005.fch")]
    [InlineData("alice_backup_restore-20260810-143005.fch")]
    public void Matches_all_four_pre_1_0_7_game_generated_backup_shapes(string fileName)
    {
        Assert.True(SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName));
    }

    /// <summary>
    /// As of 1.0.7 (confirmed changed at or before that version; nothing
    /// between 0.221.10 and 1.0.7 was checked), every producer writes one
    /// shared, hyphenated timestamp format — the auto-backup shape above
    /// (no hyphen) is no longer written, but files written by an older
    /// version before an upgrade can still be sitting on disk, so both are
    /// matched. Caught live, on a real post-upgrade auto-backup filename the
    /// corpus at the time of the 1.0.7 patch-day pass had no example of.
    /// </summary>
    [Theory]
    [InlineData("alice_backup_20260810-143005.fch")]
    [InlineData("alice_backup_auto-20260810-143005.fch")]
    [InlineData("alice_backup_cloud-20260810-143005.fch")]
    [InlineData("alice_backup_restore-20260810-143005.fch")]
    public void Matches_all_four_1_0_7_game_generated_backup_shapes(string fileName)
    {
        Assert.True(SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName));
    }

    [Theory]
    [InlineData("ALICE_BACKUP_20260810-143005.FCH")]
    [InlineData("Alice_Backup_Auto-20260810143005.fch")]
    public void Extension_is_case_insensitive_but_the_backup_markers_are_not(string fileName)
    {
        Assert.False(SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName));
    }

    [Theory]
    [InlineData("alice.fch")]
    [InlineData("aliceOld.fch")]
    [InlineData("aliceCopy.fch")]
    [InlineData("alice_backup_.fch")]
    [InlineData("alice_backup_notatimestamp.fch")]
    [InlineData("alice_backup_2026081-143005.fch")]
    [InlineData("_backup_20260810-143005.fch")]
    [InlineData("alice_backup_20260810-143005.fch.old")]
    [InlineData("alice_backup_20260810-143005.txt")]
    public void Does_not_match_non_backup_or_manually_renamed_files(string fileName)
    {
        Assert.False(SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName));
    }

    /// <summary>
    /// The world-file side of the same shapes, via the two-argument overload —
    /// confirms one regex genuinely serves both extensions, not just that
    /// the parameter
    /// compiles.
    /// </summary>
    [Theory]
    [InlineData("MyWorld_backup_20260810-143005.fwl")]
    [InlineData("MyWorld_backup_auto-20260810143005.fwl")]
    [InlineData("MyWorld_backup_auto-20260810-143005.fwl")]
    [InlineData("MyWorld_backup_cloud-20260810-143005.fwl")]
    [InlineData("MyWorld_backup_restore-20260810-143005.fwl")]
    public void Matches_all_four_game_generated_backup_shapes_for_world_files(string fileName)
    {
        Assert.True(SaveFileBackupNaming.IsGameGeneratedBackupFileName(fileName, ".fwl"));
    }

    [Fact]
    public void World_file_overload_does_not_match_a_live_world_file()
    {
        Assert.False(SaveFileBackupNaming.IsGameGeneratedBackupFileName("MyWorld.fwl", ".fwl"));
    }

    [Fact]
    public void A_character_shaped_backup_name_does_not_match_the_world_extension()
    {
        Assert.False(SaveFileBackupNaming.IsGameGeneratedBackupFileName("alice_backup_auto-20260810143005.fch", ".fwl"));
    }
}
