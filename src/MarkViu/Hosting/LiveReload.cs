namespace MarkViu.Hosting;

/// <summary>
/// Watches the displayed file and raises <see cref="Changed"/> once per save.
/// Editors often write a temp file and rename it, firing several events, so changes are debounced.
/// </summary>
public sealed class LiveReload : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    private readonly Timer _timer;
    private FileSystemWatcher? _watcher;

    public LiveReload() => _timer = new Timer(_ => Changed?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);

    /// <summary>Raised on a thread-pool thread.</summary>
    public event Action? Changed;

    public string? WatchedPath { get; private set; }

    public void Watch(string? filePath)
    {
        if (string.Equals(filePath, WatchedPath, StringComparison.OrdinalIgnoreCase))
            return;

        Stop();
        var dir = filePath is null ? null : Path.GetDirectoryName(filePath);
        if (filePath is null || dir is null || !Directory.Exists(dir))
            return;

        try
        {
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
            };
            _watcher.Changed += OnEvent;
            _watcher.Created += OnEvent;
            _watcher.Renamed += OnEvent;
            _watcher.EnableRaisingEvents = true;
            WatchedPath = filePath;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Stop(); // e.g. a network share that doesn't support change notifications
        }
    }

    private void OnEvent(object sender, FileSystemEventArgs e) => _timer.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _watcher?.Dispose();
        _watcher = null;
        WatchedPath = null;
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
