namespace MarkViu.Browsing;

/// <summary>
/// Watches the files panel's root folder, and everything below it, for files and folders that appear,
/// disappear or are renamed. Bursts of events (a checkout, an editor's save) raise <see cref="Changed"/> once.
/// </summary>
public sealed class TreeWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly Timer _timer;
    private FileSystemWatcher? _watcher;

    public TreeWatcher() => _timer = new Timer(_ => Changed?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);

    /// <summary>Raised on a thread-pool thread.</summary>
    public event Action? Changed;

    /// <summary>Filters events by path, on the watcher's thread; when null every event counts.</summary>
    public Func<string, bool>? IsRelevant { get; set; }

    public string? WatchedFolder { get; private set; }

    public void Watch(string? folder)
    {
        if (string.Equals(folder, WatchedFolder, StringComparison.OrdinalIgnoreCase))
            return;

        Stop();
        if (folder is null || !Directory.Exists(folder))
            return;

        try
        {
            _watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
            };
            _watcher.Created += OnEvent;
            _watcher.Deleted += OnEvent;
            _watcher.Renamed += OnEvent;
            _watcher.Error += (_, _) => Poke(); // buffer overflow: rescan everything
            _watcher.EnableRaisingEvents = true;
            WatchedFolder = folder;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Stop(); // e.g. a network share that doesn't support change notifications
        }
    }

    private void OnEvent(object sender, FileSystemEventArgs e)
    {
        var relevant = IsRelevant;
        if (relevant is null || relevant(e.FullPath) || (e is RenamedEventArgs r && relevant(r.OldFullPath)))
            Poke();
    }

    private void Poke() => _timer.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _watcher?.Dispose();
        _watcher = null;
        WatchedFolder = null;
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
