using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Validity/availability checks behind the save-time file-rename offer
/// (<c>MainWindow.TryOfferFileRename</c>). Deliberately exercises the
/// platform-independent superset of Windows'/Linux's illegal-filename
/// rules regardless of the host running the test — a name
/// rejected here must be rejected on both hosts, not just whichever one
/// happens to run the suite.
/// </summary>
public class SaveFileRenamingTests
{
    [Theory]
    [InlineData("Woody")]
    [InlineData("Mr. Smith")]
    [InlineData("björk")]
    public void Valid_names_pass(string candidate)
    {
        Assert.Null(SaveFileRenaming.ValidationError(candidate));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_names_are_rejected(string candidate)
    {
        Assert.NotNull(SaveFileRenaming.ValidationError(candidate));
    }

    [Theory]
    [InlineData("Woody<>")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a\"b")]
    public void Illegal_characters_are_rejected_even_though_some_are_legal_on_linux(string candidate)
    {
        Assert.NotNull(SaveFileRenaming.ValidationError(candidate));
    }

    [Theory]
    [InlineData("Woody.")]
    [InlineData("Woody ")]
    public void Trailing_period_or_space_is_rejected(string candidate)
    {
        Assert.NotNull(SaveFileRenaming.ValidationError(candidate));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL")]
    [InlineData("LPT1")]
    public void Windows_reserved_device_names_are_rejected_even_though_legal_on_linux(string candidate)
    {
        Assert.NotNull(SaveFileRenaming.ValidationError(candidate));
    }

    [Fact]
    public void Name_matching_an_existing_file_is_unavailable_case_insensitively()
    {
        using var directory = TempDirectory.Create();
        File.WriteAllBytes(Path.Combine(directory.Path, "Woody.fch"), []);

        Assert.False(SaveFileRenaming.IsNameAvailable(directory.Path, "woody", ".fch"));
        Assert.False(SaveFileRenaming.IsNameAvailable(directory.Path, "WOODY", ".fch"));
        Assert.True(SaveFileRenaming.IsNameAvailable(directory.Path, "Woody", ".dat"));
        Assert.True(SaveFileRenaming.IsNameAvailable(directory.Path, "SomeoneElse", ".fch"));
    }

    [Fact]
    public void A_missing_directory_counts_as_available()
    {
        var missing = TempFile.NonExistentPath();
        Assert.True(SaveFileRenaming.IsNameAvailable(missing, "Woody", ".fch"));
    }
}
