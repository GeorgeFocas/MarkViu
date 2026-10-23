using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkViu.Rendering;

public sealed record RenderedDocument(string Title, string BodyHtml);

public sealed partial class MarkdownRenderer
{
    // Same set as UseAdvancedExtensions() minus math (no KaTeX bundled, so $…$ stays literal),
    // plus front matter and emoji. UseDiagrams() turns ```mermaid blocks into <pre class="mermaid">.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAbbreviations()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseCitations()
        .UseCustomContainers()
        .UseDefinitionLists()
        .UseEmphasisExtras()
        .UseFigures()
        .UseFooters()
        .UseFootnotes()
        .UseGridTables()
        .UseMediaLinks()
        .UsePipeTables()
        .UseListExtras()
        .UseTaskLists()
        .UseDiagrams()
        .UseAutoLinks()
        .UseEmojiAndSmiley(enableSmileys: false)
        .UseGenericAttributes()
        .Build();

    private readonly string _template;

    public MarkdownRenderer(string template) => _template = template;

    public static RenderedDocument Render(string markdown, string fallbackTitle)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var html = document.ToHtml(Pipeline);
        return new RenderedDocument(ExtractTitle(document) ?? fallbackTitle, html);
    }

    /// <summary>Renders markdown into the full page template.</summary>
    public string RenderPage(string markdown, string fallbackTitle)
    {
        var doc = Render(markdown, fallbackTitle);
        return FillTemplate(doc.Title, doc.BodyHtml);
    }

    public string FillTemplate(string title, string bodyHtml)
    {
        var values = new Dictionary<string, string>
        {
            ["title"] = WebUtility.HtmlEncode(title),
            ["content"] = bodyHtml,
        };
        // Single pass so placeholder-like text inside the document is never re-expanded.
        return Placeholder().Replace(_template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    private static string? ExtractTitle(MarkdownDocument document)
    {
        var h1 = document.Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        if (h1?.Inline is null)
            return null;

        var sb = new StringBuilder();
        foreach (var inline in h1.Inline.Descendants<Inline>())
        {
            switch (inline)
            {
                case LiteralInline literal: sb.Append(literal.Content); break;
                case CodeInline code: sb.Append(code.Content); break;
            }
        }
        var title = Whitespace().Replace(sb.ToString(), " ").Trim();
        return title.Length > 0 ? title : null;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
