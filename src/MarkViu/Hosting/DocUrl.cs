namespace MarkViu.Hosting;

/// <summary>
/// Maps local file paths to virtual https URLs served by <see cref="DocumentServer"/> and back.
/// <code>
/// D:\docs\a b.md        ⇄ https://doc.markviu/D:/docs/a%20b.md
/// \\server\share\x.md   ⇄ https://doc.markviu/unc/server/share/x.md
/// </code>
/// Giving every document a real URL lets the browser resolve relative links and images,
/// scroll to #anchors and keep Back/Forward history on its own.
/// </summary>
public static class DocUrl
{
    public const string Host = "doc.markviu";
    public const string Origin = "https://" + Host;
    public const string Filter = Origin + "/*";

    private const string UncSegment = "unc";

    public static string FromPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            full = @"\\" + full[8..];
        else if (full.StartsWith(@"\\?\", StringComparison.Ordinal))
            full = full[4..];
        bool isDirectory = full.EndsWith('\\') || full.EndsWith('/');
        full = full.TrimEnd('\\', '/');

        string prefix;
        string rest;
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            prefix = UncSegment;
            rest = full[2..];
        }
        else if (full.Length >= 2 && char.IsAsciiLetter(full[0]) && full[1] == ':')
        {
            prefix = char.ToUpperInvariant(full[0]) + ":";
            rest = full[2..].TrimStart('\\');
        }
        else
        {
            throw new ArgumentException($"Unsupported path: {path}", nameof(path));
        }

        var segments = rest.Split('\\', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var url = Origin + "/" + string.Join('/', segments.Prepend(prefix));
        return isDirectory || rest.Length == 0 ? url + "/" : url;
    }

    public static bool TryGetPath(string url, out string path)
    {
        path = "";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && TryGetPath(uri, out path);
    }

    public static bool TryGetPath(Uri uri, out string path)
    {
        path = "";
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase))
            return false;

        var raw = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (raw.Length == 0)
            return false;

        var segments = new string[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var s = Uri.UnescapeDataString(raw[i]);
            if (s is "." or ".." || s.IndexOfAny(['\\', '/', '\0']) >= 0)
                return false;
            if (i > 0 && s.Contains(':'))
                return false;
            segments[i] = s;
        }

        bool trailingSlash = uri.AbsolutePath.EndsWith('/');
        var first = segments[0];

        if (first.Length == 2 && char.IsAsciiLetter(first[0]) && first[1] == ':')
        {
            path = first.ToUpperInvariant() + @"\" + string.Join('\\', segments.Skip(1));
        }
        else if (first.Equals(UncSegment, StringComparison.OrdinalIgnoreCase) && segments.Length >= 3)
        {
            path = @"\\" + string.Join('\\', segments.Skip(1));
        }
        else
        {
            return false;
        }

        if (trailingSlash && !path.EndsWith('\\'))
            path += @"\";
        return true;
    }
}
