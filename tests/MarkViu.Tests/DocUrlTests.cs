using MarkViu.Hosting;

namespace MarkViu.Tests;

public class DocUrlTests
{
    [Theory]
    [InlineData(@"D:\docs\readme.md", "https://doc.markviu/D:/docs/readme.md")]
    [InlineData(@"d:\docs\readme.md", "https://doc.markviu/D:/docs/readme.md")]
    [InlineData(@"C:\My Docs\a b.md", "https://doc.markviu/C:/My%20Docs/a%20b.md")]
    [InlineData(@"C:\notes\C# #1 (draft).md", "https://doc.markviu/C:/notes/C%23%20%231%20%28draft%29.md")]
    [InlineData(@"C:\café\über.md", "https://doc.markviu/C:/caf%C3%A9/%C3%BCber.md")]
    [InlineData(@"\\server\share\dir\x.md", "https://doc.markviu/unc/server/share/dir/x.md")]
    [InlineData(@"C:\", "https://doc.markviu/C:/")]
    [InlineData(@"C:\docs\", "https://doc.markviu/C:/docs/")]
    [InlineData(@"\\?\C:\long\path.md", "https://doc.markviu/C:/long/path.md")]
    [InlineData(@"\\?\UNC\server\share\x.md", "https://doc.markviu/unc/server/share/x.md")]
    public void FromPath_builds_expected_url(string path, string expected) =>
        Assert.Equal(expected, DocUrl.FromPath(path));

    [Theory]
    [InlineData(@"D:\docs\readme.md")]
    [InlineData(@"C:\My Docs\a b.md")]
    [InlineData(@"C:\notes\C# #1 (draft) 100%.md")]
    [InlineData(@"C:\café\über ñ 日本.md")]
    [InlineData(@"\\server\share\dir\x.md")]
    [InlineData(@"C:\docs\")]
    [InlineData(@"C:\")]
    public void Round_trips(string path)
    {
        Assert.True(DocUrl.TryGetPath(DocUrl.FromPath(path), out var back));
        Assert.Equal(path, back);
    }

    [Fact]
    public void Ignores_query_and_fragment()
    {
        Assert.True(DocUrl.TryGetPath("https://doc.markviu/D:/docs/a.md?x=1#intro", out var path));
        Assert.Equal(@"D:\docs\a.md", path);
    }

    [Fact]
    public void Resolves_relative_links_like_a_browser()
    {
        var page = new Uri(DocUrl.FromPath(@"D:\docs\guide\intro.md"));
        Assert.True(DocUrl.TryGetPath(new Uri(page, "../api/ref.md"), out var path));
        Assert.Equal(@"D:\docs\api\ref.md", path);
    }

    [Theory]
    [InlineData("https://doc.markviu/")]
    [InlineData("https://doc.markviu/docs/a.md")]          // escaped above the drive root
    [InlineData("https://doc.markviu/unc/server")]          // no share
    [InlineData("https://doc.markviu/D:/a%5Cb.md")]         // backslash smuggled in a segment
    [InlineData("https://doc.markviu/D:/x/C:/a.md")]        // second drive
    [InlineData("https://other.host/D:/a.md")]
    [InlineData("http://doc.markviu/D:/a.md")]
    [InlineData("not a url")]
    public void Rejects_invalid_urls(string url) =>
        Assert.False(DocUrl.TryGetPath(url, out _));
}
