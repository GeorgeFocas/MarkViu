using System.Net;
using System.Text;
using MarkViu.Rendering;

namespace MarkViu.Hosting;

public sealed record ServedResponse(int StatusCode, string ReasonPhrase, string ContentType, Stream Body);

/// <summary>
/// Answers requests for <see cref="DocUrl.Origin"/>: markdown is rendered into the page template,
/// directories show their README or a file listing, and every other file is streamed as-is.
/// </summary>
public sealed class DocumentServer
{
    private const string HtmlType = "text/html; charset=utf-8";

    private static readonly string[] MarkdownExtensions = [".md", ".markdown", ".mdown", ".mkd", ".mkdn"];
    private static readonly string[] IndexFiles = ["README.md", "readme.md", "index.md"];

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml", [".webp"] = "image/webp", [".avif"] = "image/avif", [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon", [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".ogg"] = "video/ogg",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".pdf"] = "application/pdf",
        [".txt"] = "text/plain; charset=utf-8", [".json"] = "application/json", [".css"] = "text/css",
        [".html"] = HtmlType, [".htm"] = HtmlType,
    };

    private readonly MarkdownRenderer _renderer;

    public DocumentServer(MarkdownRenderer renderer) => _renderer = renderer;

    public static bool IsMarkdown(string path) =>
        MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public ServedResponse Serve(string url)
    {
        if (!DocUrl.TryGetPath(url, out var path))
            return NotFound("(invalid address)");

        try
        {
            if (Directory.Exists(path))
                return ServeDirectory(path);
            if (!File.Exists(path))
                return NotFound(path);
            if (IsMarkdown(path))
                return Html(200, "OK", _renderer.RenderPage(ReadText(path), Path.GetFileName(path)));

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var type = MimeTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
            return new ServedResponse(200, "OK", type, stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Message(500, "Error", "Could not open file", $"`{path}`\n\n{ex.Message}");
        }
    }

    private ServedResponse ServeDirectory(string dir)
    {
        foreach (var name in IndexFiles)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
                return Html(200, "OK", _renderer.RenderPage(ReadText(candidate), name));
        }

        var info = new DirectoryInfo(dir);
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(EscapeMarkdown(info.Name.Length > 0 ? info.Name : dir)).AppendLine();
        sb.Append('`').Append(dir).AppendLine("`").AppendLine();
        if (info.Parent is not null)
            sb.AppendLine("- 📁 [..](../)");
        foreach (var sub in info.EnumerateDirectories().Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden)).OrderBy(d => d.Name))
            sb.Append("- 📁 [").Append(EscapeMarkdown(sub.Name)).Append("/](").Append(Uri.EscapeDataString(sub.Name)).AppendLine("/)");
        foreach (var file in info.EnumerateFiles().Where(f => IsMarkdown(f.Name)).OrderBy(f => f.Name))
            sb.Append("- 📄 [").Append(EscapeMarkdown(file.Name)).Append("](").Append(Uri.EscapeDataString(file.Name)).AppendLine(")");

        return Html(200, "OK", _renderer.RenderPage(sb.ToString(), info.Name));
    }

    private ServedResponse NotFound(string path) =>
        Message(404, "Not Found", "File not found", $"`{path}`\n\n<a href=\"#\" data-action=\"back\">← Go back</a>");

    private ServedResponse Message(int status, string reason, string heading, string body) =>
        Html(status, reason, _renderer.RenderPage($"# {heading}\n\n{body}\n", heading));

    private static ServedResponse Html(int status, string reason, string html) =>
        new(status, reason, HtmlType, new MemoryStream(Encoding.UTF8.GetBytes(html)));

    private static string ReadText(string path)
    {
        // Editors may hold the file open while saving; share everything so we can still read it.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string EscapeMarkdown(string text) =>
        WebUtility.HtmlEncode(text).Replace("[", "\\[").Replace("]", "\\]").Replace("_", "\\_").Replace("*", "\\*");
}
