namespace Norn.UI;

/// <summary>
/// Understands exactly the subset of Markdown Norn's bundled content files
/// are written in — <c>## Heading</c> lines, blank-line-separated paragraphs
/// (soft-wrapped lines within a paragraph are joined with a space, matching
/// how Markdown itself treats them), and <c>- </c> bullet lists. Nothing
/// else: no links, no emphasis, no nested lists. Deliberately a small
/// hand-rolled parser rather than a real Markdown library — the content this
/// reads is short and author-controlled (<c>HelpContent.md</c>,
/// <c>ReleaseNotes.md</c>), not arbitrary third-party Markdown, so there's
/// nothing here a general-purpose parser would buy that's worth a new
/// dependency for. Named for the grammar, not either caller — originally
/// <c>HelpContentParser</c>, generalized once <c>ReleaseNotesStore</c>
/// needed the identical grammar and there was nothing Help-specific in the
/// logic to begin with, only in the name. Extend the switch in
/// <see cref="Parse"/> only when some content actually needs a construct
/// this doesn't understand yet — not before.
/// </summary>
public static class MarkdownSectionParser
{
    public static IReadOnlyList<MarkdownSection> Parse(string markdown)
    {
        var sections = new List<MarkdownSection>();
        string? currentTitle = null;
        var blocks = new List<MarkdownBlock>();
        var paragraphLines = new List<string>();
        var bulletItems = new List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count > 0)
            {
                blocks.Add(new MarkdownParagraph(string.Join(' ', paragraphLines)));
                paragraphLines.Clear();
            }
        }

        void FlushBullets()
        {
            if (bulletItems.Count > 0)
            {
                blocks.Add(new MarkdownBulletList(bulletItems.ToArray()));
                bulletItems.Clear();
            }
        }

        void FlushSection()
        {
            FlushParagraph();
            FlushBullets();
            if (currentTitle is not null)
            {
                sections.Add(new MarkdownSection(currentTitle, blocks));
            }

            blocks = [];
        }

        // Text before the first "## " heading is discarded, not an error —
        // these files are entirely author-controlled, not user input, so
        // failing loudly over a leading blank line or a stray comment
        // would cost more than it protects.
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushSection();
                currentTitle = line[3..].Trim();
                continue;
            }

            if (line.Length == 0)
            {
                FlushParagraph();
                FlushBullets();
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                bulletItems.Add(line[2..].Trim());
                continue;
            }

            FlushBullets();
            paragraphLines.Add(line.Trim());
        }

        FlushSection();
        return sections;
    }
}
