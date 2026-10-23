using System.IO;
using System.Windows;
using MarkViu.Hosting;
using MarkViu.Rendering;
using MarkViu.Shell;
using Microsoft.Web.WebView2.Core;

namespace MarkViu;

public partial class App : Application
{
    public const string WelcomeUrl = "https://" + LinkRouter.AppHost + "/welcome.html";

    public static string WebRoot { get; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    public static DocumentServer Server { get; } =
        new(new MarkdownRenderer(File.ReadAllText(Path.Combine(WebRoot, "template.html"))));

    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>One shared environment so every window in the process uses the same browser instance.</summary>
    public static Task<CoreWebView2Environment> GetWebViewEnvironmentAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            // Next to the exe may be read-only, so keep browser data in the user profile.
            userDataFolder: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkViu", "WebView2"));

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;
        bool quiet = args.Contains("--quiet", StringComparer.OrdinalIgnoreCase);
        var command = args.FirstOrDefault(a => a.StartsWith("--", StringComparison.Ordinal));

        if (command is "--register" or "--unregister")
        {
            Shutdown(RunShellCommand(command == "--register", quiet));
            return;
        }

        if (!IsWebViewRuntimeInstalled())
        {
            MessageBox.Show(
                "MarkViu needs the Microsoft Edge WebView2 Runtime.\n\nDownload it from:\nhttps://developer.microsoft.com/microsoft-edge/webview2/",
                "MarkViu", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var file = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        new MainWindow(file is null ? WelcomeUrl : DocUrl.FromPath(file)).Show();
    }

    private static int RunShellCommand(bool register, bool quiet)
    {
        var registrar = new ContextMenuRegistrar();
        string message;
        try
        {
            if (register)
            {
                registrar.Register(Environment.ProcessPath!);
                message = $"\"{ContextMenuRegistrar.MenuText}\" was added to the context menu of markdown files.\n\n" +
                          "On Windows 11 it is under \"Show more options\" (or Shift+Right-click).";
            }
            else
            {
                registrar.Unregister();
                message = $"\"{ContextMenuRegistrar.MenuText}\" was removed from the context menu.";
            }
        }
        catch (Exception ex)
        {
            if (!quiet)
                MessageBox.Show(ex.Message, "MarkViu", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }

        if (!quiet)
            MessageBox.Show(message, "MarkViu", MessageBoxButton.OK, MessageBoxImage.Information);
        return 0;
    }

    private static bool IsWebViewRuntimeInstalled()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }
}
