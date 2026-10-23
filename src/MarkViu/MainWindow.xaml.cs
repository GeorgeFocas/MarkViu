using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using MarkViu.Hosting;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkViu;

public partial class MainWindow : Window
{
    private readonly string _startUrl;
    private readonly LiveReload _liveReload = new();

    public MainWindow(string startUrl)
    {
        InitializeComponent();
        _startUrl = startUrl;
        WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        _liveReload.Changed += () => Dispatcher.InvokeAsync(() => WebView.CoreWebView2?.PostWebMessageAsString("reload"));
        Loaded += async (_, _) => await InitializeWebViewAsync();
        Closed += (_, _) => _liveReload.Dispose();
    }

    private CoreWebView2 Core => WebView.CoreWebView2;

    private async Task InitializeWebViewAsync()
    {
        await WebView.EnsureCoreWebView2Async(await App.GetWebViewEnvironmentAsync());

        Core.SetVirtualHostNameToFolderMapping(LinkRouter.AppHost, App.WebRoot, CoreWebView2HostResourceAccessKind.Allow);
        Core.AddWebResourceRequestedFilter(DocUrl.Filter, CoreWebView2WebResourceContext.All);
        Core.WebResourceRequested += OnWebResourceRequested;
        Core.NavigationStarting += OnNavigationStarting;
        Core.NewWindowRequested += OnNewWindowRequested;
        Core.SourceChanged += (_, _) => OnSourceChanged();
        Core.HistoryChanged += (_, _) => UpdateHistoryButtons();
        Core.DocumentTitleChanged += (_, _) => Title = string.IsNullOrWhiteSpace(Core.DocumentTitle) ? "MarkViu" : $"{Core.DocumentTitle} – MarkViu";

        Core.Navigate(_startUrl);
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var response = App.Server.Serve(e.Request.Uri);
        e.Response = Core.Environment.CreateWebResourceResponse(
            response.Body, response.StatusCode, response.ReasonPhrase,
            $"Content-Type: {response.ContentType}\r\nCache-Control: no-store");
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var decision = LinkRouter.Decide(e.Uri);
        if (decision.Action == NavAction.Allow)
            return;

        e.Cancel = true;
        // Don't start a new navigation from inside the event that is being cancelled.
        Dispatcher.InvokeAsync(() => Execute(decision));
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // target="_blank", Ctrl+click and middle-click: documents open in another MarkViu window.
        e.Handled = true;
        var decision = LinkRouter.Decide(e.Uri);
        switch (decision.Action)
        {
            case NavAction.Allow:
                new MainWindow(e.Uri).Show();
                break;
            case NavAction.Redirect:
                new MainWindow(decision.Target!).Show();
                break;
            default:
                Execute(decision);
                break;
        }
    }

    private void Execute(NavDecision decision)
    {
        try
        {
            switch (decision.Action)
            {
                case NavAction.Redirect:
                    Core.Navigate(decision.Target!);
                    break;
                case NavAction.OpenExternal:
                case NavAction.OpenWithDefaultApp:
                    Process.Start(new ProcessStartInfo(decision.Target!) { UseShellExecute = true });
                    break;
                case NavAction.RevealInExplorer:
                    Process.Start("explorer.exe", $"/select,\"{decision.Target}\"");
                    break;
            }
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show(this, $"Could not open {decision.Target}\n\n{ex.Message}", "MarkViu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSourceChanged()
    {
        if (DocUrl.TryGetPath(Core.Source, out var path))
        {
            PathBox.Text = path;
            _liveReload.Watch(File.Exists(path) ? path : null);
        }
        else
        {
            PathBox.Text = "";
            _liveReload.Watch(null);
        }
    }

    private void UpdateHistoryButtons()
    {
        BackButton.IsEnabled = Core.CanGoBack;
        ForwardButton.IsEnabled = Core.CanGoForward;
    }

    // Key presses inside the web view are forwarded here; marking them handled stops
    // the browser from also acting on them (it has its own Alt+Left and F5).
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (WebView.CoreWebView2 is null)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;        if (modifiers == ModifierKeys.Alt && key == Key.Left)
            GoBack();
        else if (modifiers == ModifierKeys.Alt && key == Key.Right)
            GoForward();
        else if (modifiers == ModifierKeys.None && key == Key.F5)
            Core.Reload();
        else if (modifiers == ModifierKeys.Control && key == Key.O)
            ShowOpenDialog();
        else
            return;
        e.Handled = true;
    }

    private void GoBack()
    {
        if (Core.CanGoBack)
            Core.GoBack();
    }

    private void GoForward()
    {
        if (Core.CanGoForward)
            Core.GoForward();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

    private void Forward_Click(object sender, RoutedEventArgs e) => GoForward();

    private void Reload_Click(object sender, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();

    private void Open_Click(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void ShowOpenDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open markdown file",
            Filter = "Markdown files|*.md;*.markdown;*.mdown;*.mkd;*.mkdn|All files|*.*",
        };
        if (DocUrl.TryGetPath(WebView.CoreWebView2?.Source ?? "", out var current))
            dialog.InitialDirectory = Directory.Exists(current) ? current : Path.GetDirectoryName(current);

        if (dialog.ShowDialog(this) == true)
            Core.Navigate(DocUrl.FromPath(dialog.FileName));
    }
}
