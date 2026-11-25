using MarkViu.Browsing;

namespace MarkViu.Tests;

public sealed class MarkdownTreeTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("markviu-tree-").FullName;

    public void Dispose()
    {
        foreach (var info in new DirectoryInfo(_dir).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            info.Attributes = FileAttributes.Normal; // so hidden folders can be deleted
        Directory.Delete(_dir, recursive: true);
    }

    private string Touch(string relative)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private static string Shape(MarkdownNode node) =>
        string.Join(" ", node.Children.Select(c => c.IsDirectory ? $"{c.Name}/[{Shape(c)}]" : c.Name));

    [Fact]
    public void Lists_only_markdown_files()
    {
        Touch("a.md");
        Touch("b.markdown");
        Touch("notes.txt");
        Touch("pic.png");

        Assert.Equal("a.md b.markdown", Shape(MarkdownTree.Build(_dir)));
    }

    [Fact]
    public void Drops_folders_without_markdown_at_any_depth()
    {
        Touch(@"images\logo.svg");
        Touch(@"empty\deeper\still.txt");
        Touch(@"docs\guide\intro.md");
        Directory.CreateDirectory(Path.Combine(_dir, "nothing"));

        Assert.Equal("docs/[guide/[intro.md]]", Shape(MarkdownTree.Build(_dir)));
    }

    [Fact]
    public void Skips_dot_folders_dependencies_build_output_and_hidden_folders()
    {
        Touch(@".git\a.md");
        Touch(@".github\b.md");
        Touch(@"node_modules\pkg\README.md");
        Touch(@"bin\Debug\c.md");
        Touch(@"OBJ\d.md");
        Touch(@"hidden\e.md");
        File.SetAttributes(Path.Combine(_dir, "hidden"), FileAttributes.Directory | FileAttributes.Hidden);
        Touch("kept.md");

        Assert.Equal("kept.md", Shape(MarkdownTree.Build(_dir)));
    }

    [Fact]
    public void Lists_folders_first_then_files_ignoring_case()
    {
        Touch("b.md");
        Touch("A.md");
        Touch(@"z\x.md");
        Touch(@"Y\x.md");

        Assert.Equal("Y/[x.md] z/[x.md] A.md b.md", Shape(MarkdownTree.Build(_dir)));
    }

    [Fact]
    public void Returns_the_root_even_when_it_is_empty()
    {
        var root = MarkdownTree.Build(_dir + Path.DirectorySeparatorChar);

        Assert.True(root.IsDirectory);
        Assert.Equal(Path.GetFileName(_dir), root.Name);
        Assert.Equal(_dir, root.FullPath);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Missing_root_gives_an_empty_tree()
    {
        Assert.Empty(MarkdownTree.Build(Path.Combine(_dir, "gone")).Children);
    }

    [Fact]
    public void Build_stops_when_cancelled()
    {
        Touch(@"sub\a.md");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => MarkdownTree.Build(_dir, cancelled.Token));
    }

    [Theory]
    [InlineData(@"C:\docs", @"C:\docs", true)]
    [InlineData(@"C:\docs\a.md", @"C:\docs", true)]
    [InlineData(@"c:\DOCS\sub\a.md", @"C:\docs\", true)]
    [InlineData(@"C:\docs\", @"C:\docs", true)]
    [InlineData(@"C:\docs2\a.md", @"C:\docs", false)]
    [InlineData(@"C:\a.md", @"C:\docs", false)]
    [InlineData(@"C:\docs\a.md", @"C:\", true)]
    [InlineData(@"D:\docs\a.md", @"C:\docs", false)]
    [InlineData(@"\\server\share\x\a.md", @"\\server\share", true)]
    public void IsUnder_compares_whole_folder_names(string path, string root, bool expected)
    {
        Assert.Equal(expected, MarkdownTree.IsUnder(path, root));
    }

    [Fact]
    public void Normalize_drops_trailing_separators_but_keeps_drive_roots()
    {
        Assert.Equal(@"C:\docs", MarkdownTree.Normalize(@"C:\docs\"));
        Assert.Equal(@"C:\", MarkdownTree.Normalize(@"C:\"));
    }

    [Fact]
    public void MayAffect_ignores_changes_that_cannot_show_in_the_tree()
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(_dir, "old") };
        Directory.CreateDirectory(Path.Combine(_dir, "newdir"));

        Assert.True(MarkdownTree.MayAffect(Path.Combine(_dir, "a.md"), _dir, known));
        Assert.True(MarkdownTree.MayAffect(Path.Combine(_dir, "newdir"), _dir, known));
        Assert.True(MarkdownTree.MayAffect(Path.Combine(_dir, "old"), _dir, known)); // a deleted folder
        Assert.False(MarkdownTree.MayAffect(Path.Combine(_dir, "a.txt"), _dir, known));
        Assert.False(MarkdownTree.MayAffect(Path.Combine(_dir, @".git\x.md"), _dir, known));
        Assert.False(MarkdownTree.MayAffect(Path.Combine(_dir, @"node_modules\p\README.md"), _dir, known));
        Assert.False(MarkdownTree.MayAffect(Path.Combine(Path.GetTempPath(), "elsewhere.md"), _dir, known));
    }

    [Fact]
    public void Tree_items_find_paths_and_reveal_them()
    {
        var page = Touch(@"sub\deep\page.md");
        Touch("top.md");
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(_dir, "sub") };

        var root = TreeItem.From(MarkdownTree.Build(_dir), expanded);
        var item = root.Find(page.ToUpperInvariant())!;

        Assert.Equal("page.md", item.Name);
        Assert.True(item.Parent!.Parent!.IsExpanded); // kept from the previous tree
        Assert.False(item.Parent.IsExpanded);
        item.Reveal();
        Assert.True(item.Parent.IsExpanded);
        Assert.Null(root.Find(Path.Combine(_dir, "missing.md")));
    }
}
