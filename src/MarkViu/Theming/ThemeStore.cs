using System.Text.Json;

namespace MarkViu.Theming;

/// <summary>
/// Keeps the theme preference in a small JSON file, for example <c>{ "theme": "dark" }</c>.
/// Settings are a convenience, so an unreadable or unwritable file never stops the viewer:
/// loading falls back to <see cref="ThemePreference.System"/> and a failed save is ignored.
/// </summary>
public sealed class ThemeStore(string path)
{
    private const string ThemeKey = "theme";
    private const int ReplaceAttempts = 10;
    private const int ReplaceRetryDelayMs = 20;

    public ThemePreference Load() => TryLoad(out var preference) ? preference : ThemePreference.System;

    /// <summary>
    /// Reads the saved preference. A missing, corrupt or unknown setting counts as
    /// <see cref="ThemePreference.System"/>; false means the file could not be read, so what it says is unknown.
    /// </summary>
    public bool TryLoad(out ThemePreference preference)
    {
        preference = ThemePreference.System;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty(ThemeKey, out var theme)
                && theme.ValueKind == JsonValueKind.String)
            {
                preference = theme.GetString() switch
                {
                    "light" => ThemePreference.Light,
                    "dark" => ThemePreference.Dark,
                    _ => ThemePreference.System,
                };
            }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Save(ThemePreference preference)
    {
        // Replace the file in one step, because other MarkViu processes read it as soon as it changes.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temp, JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [ThemeKey] = preference.ToString().ToLowerInvariant(),
            }));
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
                File.Move(temp, path, overwrite: true);
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
