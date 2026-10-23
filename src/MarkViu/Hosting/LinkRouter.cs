namespace MarkViu.Hosting;

public enum NavAction
{
    /// <summary>Let the web view navigate.</summary>
    Allow,
    /// <summary>Cancel and do nothing.</summary>
    Block,
    /// <summary>Cancel and navigate to <see cref="NavDecision.Target"/> instead.</summary>
    Redirect,
    /// <summary>Cancel and open the URL in the default browser / mail client.</summary>
    OpenExternal,
    /// <summary>Cancel and open the local file with its default app.</summary>
    OpenWithDefaultApp,
    /// <summary>Cancel and select the file in Explorer (used for executables).</summary>
    RevealInExplorer,
}

public readonly record struct NavDecision(NavAction Action, string? Target = null);

/// <summary>Decides what happens when the user follows a link.</summary>
public static class LinkRouter
{
    public const string AppHost = "app.markviu";

    private static readonly HashSet<string> ExternalSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" };

    // Never launch these straight from a link in a document; show them in Explorer instead.
    private static readonly HashSet<string> RunnableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msi",
        ".msp", ".scr", ".pif", ".lnk", ".url", ".hta", ".cpl", ".reg", ".jar", ".appref-ms", ".application",
    };

    public static NavDecision Decide(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new(NavAction.Block);

        if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(DocUrl.Host, StringComparison.OrdinalIgnoreCase))
            return DecideDocument(uri);

        if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(AppHost, StringComparison.OrdinalIgnoreCase))
            return new(NavAction.Allow);

        if (uri.IsFile)
        {
            var path = uri.LocalPath;
            if (DocumentServer.IsMarkdown(path) || Directory.Exists(path) || !File.Exists(path))
                return new(NavAction.Redirect, DocUrl.FromPath(path) + uri.Fragment);
            return OpenLocalFile(path);
        }

        if (uri.AbsoluteUri == "about:blank")
            return new(NavAction.Allow);

        return ExternalSchemes.Contains(uri.Scheme)
            ? new(NavAction.OpenExternal, uri.AbsoluteUri)
            : new(NavAction.Block);
    }

    private static NavDecision DecideDocument(Uri uri)
    {
        if (!DocUrl.TryGetPath(uri, out var path))
            return new(NavAction.Allow); // the server answers with a "not found" page

        if (Directory.Exists(path))
        {
            // Relative links inside a folder page only resolve correctly with a trailing slash.
            return uri.AbsolutePath.EndsWith('/')
                ? new(NavAction.Allow)
                : new(NavAction.Redirect, uri.GetLeftPart(UriPartial.Path) + "/" + uri.Query + uri.Fragment);
        }

        if (DocumentServer.IsMarkdown(path) || !File.Exists(path))
            return new(NavAction.Allow);

        return OpenLocalFile(path);
    }

    private static NavDecision OpenLocalFile(string path) =>
        RunnableExtensions.Contains(Path.GetExtension(path))
            ? new(NavAction.RevealInExplorer, path)
            : new(NavAction.OpenWithDefaultApp, path);
}
