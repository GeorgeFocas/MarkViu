using MarkViu.Hosting;
using MarkViu.Rendering;

namespace MarkViu.Tests;

public sealed class DocumentServerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("markviu-server-").FullName;
    private readonly DocumentServer _server = new(new MarkdownRenderer("<title>{{title}}</title>{{content}}"));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Url(string relative) => DocUrl.FromPath(Path.Combine(_dir, relative));

    private static string ReadBody(ServedResponse response)
    {
        using var reader = new StreamReader(response.Body);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Renders_markdown()
    {
        File.WriteAllText(Path.Combine(_dir, "a.md"), "# Title\n\nHello");
        var response = _server.Serve(Url("a.md"));
        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith("text/html", response.ContentType);
        Assert.Contains("<title>Title</title>", ReadBody(response));
    }

    [Fact]
    public void Reads_files_held_open_by_an_editor()
    {
        var path = Path.Combine(_dir, "open.md");
        File.WriteAllText(path, "locked");
        using var editor = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        Assert.Contains("locked", ReadBody(_server.Serve(Url("open.md"))));
    }

    [Fact]
    public void Serves_other_files_with_mime_type()
    {
        File.WriteAllBytes(Path.Combine(_dir, "pic.PNG"), [1, 2, 3]);
        var response = _server.Serve(Url("pic.PNG"));
        Assert.Equal("image/png", response.ContentType);
        using var ms = new MemoryStream();
        using (response.Body) response.Body.CopyTo(ms);
        Assert.Equal([1, 2, 3], ms.ToArray());
    }

    [Fact]
    public void Missing_file_returns_404_page()
    {
        var response = _server.Serve(Url("nope.md"));
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("File not found", ReadBody(response));
    }

    [Fact]
    public void Folder_shows_its_readme()
    {
        File.WriteAllText(Path.Combine(_dir, "README.md"), "# Readme here");
        Assert.Contains("Readme here", ReadBody(_server.Serve(Url(""))));
    }

    [Fact]
    public void Folder_without_readme_lists_markdown_files_and_subfolders()
    {
        File.WriteAllText(Path.Combine(_dir, "b c.md"), "x");
        File.WriteAllText(Path.Combine(_dir, "skip.txt"), "x");
        Directory.CreateDirectory(Path.Combine(_dir, "sub dir"));

        var body = ReadBody(_server.Serve(Url("")));
        Assert.Contains("href=\"b%20c.md\"", body);
        Assert.Contains("href=\"sub%20dir/\"", body);
        Assert.Contains("href=\"../\"", body);
        Assert.DoesNotContain("skip.txt", body);
    }
}
