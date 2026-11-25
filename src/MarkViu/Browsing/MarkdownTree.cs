using MarkViu.Hosting;

namespace MarkViu.Browsing;

public sealed record MarkdownNode(string Name, string FullPath, bool IsDirectory, IReadOnlyList<MarkdownNode> Children);

/// <summary>
/// Scans a folder for the files panel: only markdown files, and only the folders that lead to some.
/// Hidden folders and ones that are usually huge and never hold docs (.git, node_modules, bin, obj) are skipped.
/// </summary>
public static class MarkdownTree
{
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj" };

    private static readonly StringComparer NameOrder = StringComparer.CurrentCultureIgnoreCase;

    /// <summary>Scans <paramref name="root"/>. The root itself is always returned, even when it holds no markdown.</summary>
    public static MarkdownNode Build(string root, CancellationToken cancellationToken = default)
    {
        var full = Normalize(root);
        var info = new DirectoryInfo(full);
        return new MarkdownNode(info.Name.Length > 0 ? info.Name : full, full, true, ScanChildren(info, cancellationToken));
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> or inside it.</summary>
    public static bool IsUnder(string path, string root)
    {
        var p = Normalize(path);
        var r = Normalize(root);
        if (p.Equals(r, StringComparison.OrdinalIgnoreCase))
            return true;
        var prefix = r.EndsWith('\\') ? r : r + '\\';
        return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Full path without a trailing separator, except for a drive root such as <c>C:\</c>.</summary>
    public static string Normalize(string path)
    {
        var trimmed = Path.GetFullPath(path).TrimEnd('\\', '/');
        return trimmed.Length == 2 && trimmed[1] == ':' ? trimmed + '\\' : trimmed;
    }

    /// <summary>
    /// Whether a file-system change at <paramref name="path"/> could change the tree of <paramref name="root"/>,
    /// so churn in .git, build output or temp files doesn't cause rescans.
    /// </summary>
    /// <param name="knownFolders">Folders shown in the tree now; a deleted folder no longer exists to check.</param>
    public static bool MayAffect(string path, string root, IReadOnlySet<string> knownFolders)
    {
        if (!IsUnder(path, root))
            return false;
        var folders = Path.GetRelativePath(root, path).Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).SkipLast(1);
        if (folders.Any(s => !IsBrowsableName(s)))
            return false;
        return DocumentServer.IsMarkdown(path) || knownFolders.Contains(Normalize(path)) || Directory.Exists(path);
    }

    private static List<MarkdownNode> ScanChildren(DirectoryInfo dir, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var children = new List<MarkdownNode>();
        try
        {
            foreach (var sub in dir.EnumerateDirectories().Where(IsBrowsable).OrderBy(d => d.Name, NameOrder))
            {
                var grandChildren = ScanChildren(sub, cancellationToken);
                if (grandChildren.Count > 0)
                    children.Add(new MarkdownNode(sub.Name, sub.FullName, true, grandChildren));
            }
            foreach (var file in dir.EnumerateFiles().Where(f => DocumentServer.IsMarkdown(f.Name)).OrderBy(f => f.Name, NameOrder))
                children.Add(new MarkdownNode(file.Name, file.FullName, false, []));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // A folder we may not list (or one that vanished mid-scan) just shows what was found so far.
        }
        return children;
    }

    private static bool IsBrowsableName(string name) => !name.StartsWith('.') && !SkippedFolders.Contains(name);

    private static bool IsBrowsable(DirectoryInfo dir) =>
        IsBrowsableName(dir.Name)
        && (dir.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) == 0;
}
