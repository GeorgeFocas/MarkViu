using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MarkViu.Browsing;
using MarkViu.Hosting;
using MarkViu.Settings;
using MarkViu.Theming;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkViu;

public partial class MainWindow : Window
{
    private readonly string _startUrl;
    private readonly LiveReload _liveReload = new();
    private readonly TreeWatcher _treeWatcher = new();

    private double _sidebarWidth;
    private string? _currentPath;           // the document on show, if it is a local path
    private string? _treeRoot;              // the folder the files panel shows
    private TreeItem? _treeRootItem;
    private bool _treeStale;                // the root changed while the panel was hidden
    private CancellationTokenSource? _scanCancellation;
    private IReadOnlySet<string> _treeFolders = FrozenSet<string>.Empty; // read by the watcher thread

    public MainWindow(string startUrl)
    {
        InitializeComponent();
        _startUrl = startUrl;
        WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        _liveReload.Changed += () => Dispatcher.InvokeAsync(() => WebView.CoreWebView2?.PostWebMessageAsString("reload"));
        _treeWatcher.IsRelevant = path => _treeRoot is { } root && MarkdownTree.MayAffect(path, root, _treeFolders);
        _treeWatcher.Changed += () => Dispatcher.InvokeAsync(RefreshTree);
        ApplyTheme();
        App.Theme.Changed += ApplyTheme;

        var layout = App.Sidebar.Load();
        _sidebarWidth = layout.Width;
        SetSidebarVisible(layout.Visible, save: false);
        UpdateTreeHeader();

        Loaded += async (_, _) => await InitializeWebViewAsync();
        Closed += (_, _) =>
        {
            App.Theme.Changed -= ApplyTheme;
            _liveReload.Dispose();
            _treeWatcher.Dispose();
            _scanCancellation?.Cancel();
        };
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

        ApplyTheme(); // before navigating, so the first paint uses the right theme
        Core.Navigate(_startUrl);
    }

    private void ApplyTheme()
    {
        bool isDark = App.Theme.IsDark;
        ThemeButton.Content = isDark ? "" : ""; // sun : moon, the theme a click switches to
        ThemeButton.ToolTip = $"Switch to {(isDark ? "light" : "dark")} theme (Ctrl+Shift+L)";

        // Page styles, code highlighting and Mermaid all key off prefers-color-scheme, which this overrides.
        if (WebView.CoreWebView2 is { } core)
        {
            core.Profile.PreferredColorScheme = App.Theme.Preference switch
            {
                ThemePreference.Light => CoreWebView2PreferredColorScheme.Light,
                ThemePreference.Dark => CoreWebView2PreferredColorScheme.Dark,
                _ => CoreWebView2PreferredColorScheme.Auto,
            };
        }
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
            _currentPath = MarkdownTree.Normalize(path);
            SyncTreeWithDocument();
        }
        else
        {
            PathBox.Text = "";
            _liveReload.Watch(null);
            _currentPath = null; // e.g. the welcome page: the panel keeps showing what it showed
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
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Alt && key == Key.Left)
            GoBack();
        else if (modifiers == ModifierKeys.Alt && key == Key.Right)
            GoForward();
        else if (modifiers == ModifierKeys.None && key == Key.F5)
            Core.Reload();
        else if (modifiers == ModifierKeys.Control && key == Key.O)
            ShowOpenDialog();
        else if (modifiers == ModifierKeys.Control && key == Key.B)
            ToggleSidebar();
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.L)
            App.Theme.Toggle();
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

    private void Theme_Click(object sender, RoutedEventArgs e) => App.Theme.Toggle();

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

    // ---- Files panel ----

    private bool IsSidebarVisible => Sidebar.Visibility == Visibility.Visible;

    private void Sidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebar();

    private void ToggleSidebar() => SetSidebarVisible(!IsSidebarVisible, save: true);

    private void SetSidebarVisible(bool visible, bool save)
    {
        if (!visible && IsSidebarVisible && SidebarColumn.ActualWidth > 0)
            _sidebarWidth = SidebarStore.ClampWidth(SidebarColumn.ActualWidth);

        Sidebar.Visibility = SidebarSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.MinWidth = visible ? SidebarStore.MinWidth : 0;
        SidebarColumn.MaxWidth = visible ? SidebarStore.MaxWidth : double.PositiveInfinity;
        SidebarColumn.Width = new GridLength(visible ? _sidebarWidth : 0);

        if (visible && _treeStale)
            RefreshTree();
        if (save)
            App.Sidebar.Save(new SidebarLayout(visible, _sidebarWidth));
    }

    private void SidebarSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _sidebarWidth = SidebarStore.ClampWidth(SidebarColumn.ActualWidth);
        App.Sidebar.Save(new SidebarLayout(true, _sidebarWidth));
    }

    /// <summary>Shows the current document in the panel, switching to its folder when it is outside the one shown.</summary>
    private void SyncTreeWithDocument()
    {
        if (_currentPath is not { } path)
            return;

        if (_treeRoot is null || !MarkdownTree.IsUnder(path, _treeRoot))
            SetTreeRoot(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
        else
            SelectCurrentDocument();
    }

    private void SetTreeRoot(string folder)
    {
        _treeRoot = MarkdownTree.Normalize(folder);
        _treeRootItem = null;
        FileTree.ItemsSource = null;
        UpdateTreeHeader();
        RefreshTree();
    }

    /// <summary>Rescans the root, keeping open folders open. While the panel is hidden this only marks it out of date.</summary>
    private async void RefreshTree()
    {
        if (_treeRoot is not { } root)
            return;
        if (!IsSidebarVisible)
        {
            _treeStale = true;
            return;
        }
        _treeStale = false;

        _scanCancellation?.Cancel();
        var cancellation = _scanCancellation = new CancellationTokenSource();
        _treeWatcher.Watch(root);

        var expanded = _treeRootItem?.Descendants().Where(i => i.IsExpanded).Select(i => i.FullPath)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MarkdownNode node;
        try
        {
            node = await Task.Run(() => MarkdownTree.Build(root, cancellation.Token), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return; // a newer scan replaced this one
        }
        if (cancellation.IsCancellationRequested)
            return;

        _treeRootItem = TreeItem.From(node, expanded);
        _treeFolders = _treeRootItem.Descendants().Where(i => i.IsDirectory).Select(i => i.FullPath)
            .Prepend(root).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        FileTree.ItemsSource = _treeRootItem.Children;
        UpdateTreeHeader();
        SelectCurrentDocument();
    }

    private void SelectCurrentDocument()
    {
        if (_treeRootItem is null)
            return;

        var item = _currentPath is null ? null : _treeRootItem.Find(_currentPath);
        if (item is null)
        {
            if (FileTree.SelectedItem is TreeItem selected)
                selected.IsSelected = false;
            return;
        }
        item.Reveal();
        item.IsSelected = true;
    }

    private void UpdateTreeHeader()
    {
        var rootName = _treeRoot is null ? null : Path.GetFileName(_treeRoot);
        RootLabel.Text = _treeRoot is null ? "Files" : string.IsNullOrEmpty(rootName) ? _treeRoot : rootName; // C:\ has no name
        RootLabel.ToolTip = _treeRoot;
        UpButton.IsEnabled = _treeRoot is not null && Directory.GetParent(_treeRoot) is not null;

        string? hint = null;
        if (_treeRoot is null)
            hint = "Open a markdown file or a folder to see its files here.";
        else if (_treeRootItem is { Children.Count: 0 })
            hint = "No markdown files in this folder.";
        TreeHint.Text = hint ?? "";
        TreeHint.Visibility = hint is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TreeUp_Click(object sender, RoutedEventArgs e)
    {
        if (_treeRoot is not null && Directory.GetParent(_treeRoot) is { } parent)
            SetTreeRoot(parent.FullName);
    }

    private void TreeRefresh_Click(object sender, RoutedEventArgs e) => RefreshTree();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Show the markdown files of a folder" };
        if (_treeRoot is not null)
            dialog.InitialDirectory = _treeRoot;
        if (dialog.ShowDialog(this) != true)
            return;

        SetTreeRoot(dialog.FolderName);
        // The folder's README, or its file listing; the folder is now the root, so the panel stays put.
        if (WebView.CoreWebView2 is not null)
            Core.Navigate(DocUrl.FromPath(dialog.FolderName + Path.DirectorySeparatorChar));
    }

    // Navigation follows clicks and Enter, not selection changes, so selecting from code never navigates.
    private void FileTree_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)
            || ItemAt(e.OriginalSource) is not { } item
            || IsWithin<ToggleButton>(e.OriginalSource)) // the expander arrow does its own thing
            return;

        if (item.IsDirectory)
        {
            if (e.ChangedButton == MouseButton.Left)
                item.IsExpanded = !item.IsExpanded;
            return;
        }
        bool newWindow = e.ChangedButton == MouseButton.Middle || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        OpenFromTree(item, newWindow);
    }

    // A single click already opens and closes folders; without this a double-click would toggle them three times.
    private void FileTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2 && ItemAt(e.OriginalSource) is { IsDirectory: true } && !IsWithin<ToggleButton>(e.OriginalSource))
            e.Handled = true;
    }

    private void FileTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || FileTree.SelectedItem is not TreeItem item)
            return;
        if (item.IsDirectory)
            item.IsExpanded = !item.IsExpanded;
        else
            OpenFromTree(item, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        e.Handled = true;
    }

    private void FileTree_ItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem container)
            container.BringIntoView();
    }

    private void OpenFromTree(TreeItem item, bool newWindow)
    {
        var url = DocUrl.FromPath(item.FullPath);
        if (newWindow)
            new MainWindow(url).Show();
        else if (WebView.CoreWebView2 is not null && !string.Equals(item.FullPath, _currentPath, StringComparison.OrdinalIgnoreCase))
            Core.Navigate(url);
    }

    private static TreeItem? ItemAt(object source) =>
        FindAncestor<TreeViewItem>(source)?.DataContext as TreeItem;

    private static bool IsWithin<T>(object source) where T : DependencyObject => FindAncestor<T>(source) is not null;

    private static T? FindAncestor<T>(object source) where T : DependencyObject
    {
        for (var d = source as DependencyObject; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is T match)
                return match;
            if (d is TreeView)
                return null;
        }
        return null;
    }
}
