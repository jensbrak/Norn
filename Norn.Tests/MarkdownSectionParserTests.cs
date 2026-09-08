using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// <see cref="MarkdownSectionParser.Parse"/> is pure, no I/O — exercised
/// directly against small hand-written Markdown snippets, not the real
/// <c>HelpContent.md</c>/<c>ReleaseNotes.md</c> (those files are content,
/// expected to keep changing; tying tests to their actual prose would make
/// every wording edit a test change too).
/// </summary>
public class MarkdownSectionParserTests
{
    [Fact]
    public void Empty_input_produces_no_sections()
    {
        Assert.Empty(MarkdownSectionParser.Parse(""));
    }

    [Fact]
    public void Text_before_the_first_heading_is_discarded()
    {
        var sections = MarkdownSectionParser.Parse("Stray text\n\n## Heading\nBody.");

        var section = Assert.Single(sections);
        Assert.Equal("Heading", section.Title);
    }

    [Fact]
    public void One_heading_and_one_paragraph()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\nSome text.");

        var section = Assert.Single(sections);
        Assert.Equal("Heading", section.Title);
        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(section.Blocks));
        Assert.Equal("Some text.", paragraph.Text);
    }

    [Fact]
    public void Soft_wrapped_lines_join_into_one_paragraph_with_spaces()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\nLine one\nline two\nline three.");

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(sections[0].Blocks));
        Assert.Equal("Line one line two line three.", paragraph.Text);
    }

    [Fact]
    public void Blank_line_separates_two_paragraphs()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\nFirst.\n\nSecond.");

        Assert.Equal(2, sections[0].Blocks.Count);
        Assert.Equal("First.", Assert.IsType<MarkdownParagraph>(sections[0].Blocks[0]).Text);
        Assert.Equal("Second.", Assert.IsType<MarkdownParagraph>(sections[0].Blocks[1]).Text);
    }

    [Fact]
    public void Consecutive_bullet_lines_become_one_list_block()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\n- One\n- Two\n- Three");

        var bullets = Assert.IsType<MarkdownBulletList>(Assert.Single(sections[0].Blocks));
        Assert.Equal(["One", "Two", "Three"], bullets.Items);
    }

    [Fact]
    public void A_paragraph_can_follow_a_bullet_list_as_its_own_block()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\n- One\n- Two\n\nAfter.");

        Assert.Equal(2, sections[0].Blocks.Count);
        Assert.IsType<MarkdownBulletList>(sections[0].Blocks[0]);
        Assert.Equal("After.", Assert.IsType<MarkdownParagraph>(sections[0].Blocks[1]).Text);
    }

    [Fact]
    public void Multiple_sections_parse_independently()
    {
        var sections = MarkdownSectionParser.Parse("## First\nOne.\n\n## Second\nTwo.");

        Assert.Equal(2, sections.Count);
        Assert.Equal("First", sections[0].Title);
        Assert.Equal("Second", sections[1].Title);
        Assert.Equal("One.", Assert.IsType<MarkdownParagraph>(sections[0].Blocks[0]).Text);
        Assert.Equal("Two.", Assert.IsType<MarkdownParagraph>(sections[1].Blocks[0]).Text);
    }

    [Fact]
    public void Crlf_line_endings_parse_the_same_as_lf()
    {
        var sections = MarkdownSectionParser.Parse("## Heading\r\nLine one\r\nline two");

        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(sections[0].Blocks));
        Assert.Equal("Line one line two", paragraph.Text);
    }
}
