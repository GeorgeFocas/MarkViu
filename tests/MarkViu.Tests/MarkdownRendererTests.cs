using MarkViu.Rendering;

namespace MarkViu.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Headings_get_github_style_ids()
    {
        var doc = MarkdownRenderer.Render("## Getting Started: Step 1", "x.md");
        Assert.Contains("id=\"getting-started-step-1\"", doc.BodyHtml);
    }

    [Fact]
    public void Mermaid_blocks_are_marked_for_rendering_and_escaped()
    {
        var doc = MarkdownRenderer.Render("```mermaid\ngraph TD\n  A[\"<img onerror=x>\"]-->B\n```", "x.md");
        Assert.Contains("<pre class=\"mermaid\">graph TD", doc.BodyHtml);
        Assert.Contains("&lt;img onerror=x>", doc.BodyHtml);
        Assert.DoesNotContain("<code", doc.BodyHtml);
    }

    [Fact]
    public void Code_blocks_keep_language_class()
    {
        var doc = MarkdownRenderer.Render("```csharp\nvar x = 1;\n```", "x.md");
        Assert.Contains("class=\"language-csharp\"", doc.BodyHtml);
    }

    [Fact]
    public void Relative_links_are_kept_verbatim()
    {
        var doc = MarkdownRenderer.Render("[next](<sub/other page.md#part-2>)", "x.md");
        Assert.Contains("href=\"sub/other%20page.md#part-2\"", doc.BodyHtml);
    }

    [Fact]
    public void Task_lists_and_tables_render()
    {
        var doc = MarkdownRenderer.Render("- [x] done\n- [ ] todo\n\n| a | b |\n|---|---|\n| 1 | 2 |", "x.md");
        Assert.Contains("type=\"checkbox\"", doc.BodyHtml);
        Assert.Contains("<table>", doc.BodyHtml);
    }

    [Fact]
    public void Front_matter_is_not_rendered()
    {
        var doc = MarkdownRenderer.Render("---\ntitle: hidden\n---\n# Visible", "x.md");
        Assert.DoesNotContain("hidden", doc.BodyHtml);
    }

    [Fact]
    public void Dollar_signs_stay_literal()
    {
        var doc = MarkdownRenderer.Render("costs $5 or $x$", "x.md");
        Assert.Contains("$x$", doc.BodyHtml);
    }

    [Theory]
    [InlineData("# Hello `World`\n\ntext", "Hello World")]
    [InlineData("intro\n\n# **Bold** title", "Bold title")]
    [InlineData("# Spaced <span>out</span>  title", "Spaced out title")]
    [InlineData("## Only h2", "fallback.md")]
    public void Title_comes_from_first_h1(string markdown, string expected) =>
        Assert.Equal(expected, MarkdownRenderer.Render(markdown, "fallback.md").Title);

    [Fact]
    public void Template_is_filled_once_and_title_escaped()
    {
        var renderer = new MarkdownRenderer("<title>{{title}}</title><main>{{content}}</main>");
        var page = renderer.RenderPage("# A `<b>` & {{content}}\n\nliteral {{title}}", "x.md");
        Assert.StartsWith("<title>A &lt;b&gt; &amp; {{content}}</title>", page);
        Assert.Contains("literal {{title}}", page);
    }
}
