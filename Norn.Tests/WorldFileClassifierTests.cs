using Norn.Adapter;

namespace Norn.Tests;

/// <summary>Confirms the Adapter seam forwards to <c>GameCore.SaveFileBackupNaming</c>
/// with the <c>.fwl</c> extension — mirrors <see cref="SaveFileClassifierTests"/>.</summary>
public class WorldFileClassifierTests
{
    [Theory]
    [InlineData("MyWorld_backup_auto-20260810143005.fwl", true)]
    [InlineData("MyWorld.fwl", false)]
    public void Forwards_to_the_game_core_predicate(string fileName, bool expected)
    {
        Assert.Equal(expected, WorldFileClassifier.IsBackupFile(fileName));
    }
}
