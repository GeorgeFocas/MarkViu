using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarkViu.Settings;

/// <summary>
/// The per-user settings file, a small JSON object shared by several stores (theme, files panel…).
/// Each store changes only its own keys, so an update reads the file, changes it and replaces it in one step.
/// Settings are a convenience: failures never throw.
/// </summary>
public sealed class SettingsFile(string path)
{
    private const int ReplaceAttempts = 10;
    private const int ReplaceRetryDelayMs = 20;

    public string Path { get; } = path;

    /// <summary>
    /// Reads the settings. A missing or corrupt file gives an empty object; null means the file
    /// exists but could not be read, so what it says is unknown.
    /// </summary>
    public JsonObject? TryRead()
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(Path)) as JsonObject ?? [];
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Changes some keys and keeps the others. Gives up quietly if the file can't be read or replaced.</summary>
    public void Update(Action<JsonObject> change)
    {
        // Writing over a file we could not read would erase the other stores' keys.
        if (TryRead() is not { } settings)
            return;
        change(settings);

        // Replace the file in one step, because other MarkViu processes read it as soon as it changes.
        var temp = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.WriteAllText(temp, settings.ToJsonString());
            ReplaceWithRetry(temp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
        }
    }

    // Windows refuses to replace a file while it is open, and another process may be reading it for a moment.
    private void ReplaceWithRetry(string temp)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, Path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < ReplaceAttempts)
            {
                Thread.Sleep(ReplaceRetryDelayMs);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
