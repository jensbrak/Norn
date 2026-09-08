namespace Norn.UI;

/// <summary>
/// One `## Heading`-delimited section of a Markdown document, parsed by
/// <see cref="MarkdownSectionParser"/> — shared by <see cref="HelpStore"/>
/// (<c>HelpContent.md</c>) and <see cref="ReleaseNotesStore"/>
/// (<c>ReleaseNotes.md</c>): both are just "sections of prose + bullets
/// under a heading," the same grammar, only the meaning of the heading text
/// and how a caller picks which section(s) to show differ. A block, not a
/// raw string, per section — a section's blocks render as paragraphs and
/// bullet lists differently, so the structure needs to survive parsing
/// rather than being flattened to text immediately.
/// </summary>
public sealed record MarkdownSection(string Title, IReadOnlyList<MarkdownBlock> Blocks);

/// <summary>Base type for one paragraph or bullet list within a
/// <see cref="MarkdownSection"/>. Not a class hierarchy meant to grow much —
/// <see cref="MarkdownSectionParser"/>'s whole point is understanding only
/// the small subset of Markdown its callers actually need, not becoming a
/// general parser as more block types get added speculatively.</summary>
public abstract record MarkdownBlock;

public sealed record MarkdownParagraph(string Text) : MarkdownBlock;

public sealed record MarkdownBulletList(IReadOnlyList<string> Items) : MarkdownBlock;
