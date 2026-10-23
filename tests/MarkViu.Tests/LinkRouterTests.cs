using MarkViu.Hosting;

namespace MarkViu.Tests;

public sealed class LinkRouterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("markviu-router-").FullName;

    public LinkRouterTests()
    {
        File.WriteAllText(Path.Combine(_dir, "doc.md"), "# Doc");
        File.WriteAllText(Path.Combine(_dir, "report.pdf"), "pdf");
        File.WriteAllText(Path.Combine(_dir, "setup.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Url(string relative) => DocUrl.FromPath(Path.Combine(_dir, relative));

    [Fact]
    public void Markdown_documents_navigate_in_app() =>
        Assert.Equal(new NavDecision(NavAction.Allow), LinkRouter.Decide(Url("doc.md") + "#intro"));

    [Fact]
    public void Missing_files_navigate_to_not_found_page() =>
        Assert.Equal(NavAction.Allow, LinkRouter.Decide(Url("missing.md")).Action);

    [Fact]
    public void Folders_without_trailing_slash_are_redirected()
    {
        var decision = LinkRouter.Decide(Url("sub") + "#x");
        Assert.Equal(NavAction.Redirect, decision.Action);
        Assert.Equal(Url("sub") + "/#x", decision.Target);
        Assert.Equal(NavAction.Allow, LinkRouter.Decide(Url("sub") + "/").Action);
    }

    [Fact]
    public void Other_local_files_open_with_default_app() =>
        Assert.Equal(new NavDecision(NavAction.OpenWithDefaultApp, Path.Combine(_dir, "report.pdf")), LinkRouter.Decide(Url("report.pdf")));

    [Fact]
    public void Executables_are_only_revealed() =>
        Assert.Equal(NavAction.RevealInExplorer, LinkRouter.Decide(Url("setup.exe")).Action);

    [Fact]
    public void File_urls_to_markdown_are_redirected_to_doc_urls()
    {
        var decision = LinkRouter.Decide(new Uri(Path.Combine(_dir, "doc.md")).AbsoluteUri + "#top");
        Assert.Equal(new NavDecision(NavAction.Redirect, Url("doc.md") + "#top"), decision);
    }

    [Theory]
    [InlineData("https://github.com/markdig")]
    [InlineData("http://example.com/a?b=c")]
    [InlineData("mailto:someone@example.com")]
    public void Web_links_open_externally(string url) =>
        Assert.Equal(NavAction.OpenExternal, LinkRouter.Decide(url).Action);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("vbscript:foo")]
    [InlineData("relative/path.md")]
    public void Unknown_schemes_are_blocked(string url) =>
        Assert.Equal(NavAction.Block, LinkRouter.Decide(url).Action);

    [Fact]
    public void App_pages_are_allowed() =>
        Assert.Equal(NavAction.Allow, LinkRouter.Decide("https://app.markviu/welcome.html").Action);
}
