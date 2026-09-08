using Norn.Adapter;

namespace Norn.Tests;

/// <summary>Confirms the Adapter seam forwards to <c>GameCore.SaveFileBackupNaming</c> unchanged.</summary>
public class SaveFileClassifierTests
{
    [Theory]
    [InlineData("alice_backup_auto-20260810143005.fch", true)]
    [InlineData("alice.fch", false)]
    public void Forwards_to_the_game_core_predicate(string fileName, bool expected)
    {
        Assert.Equal(expected, SaveFileClassifier.IsBackupFile(fileName));
    }
}
