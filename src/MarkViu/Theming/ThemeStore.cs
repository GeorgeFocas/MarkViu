using System.Text.Json;
using MarkViu.Settings;

namespace MarkViu.Theming;

/// <summary>
/// Keeps the theme preference in the settings file, for example <c>{ "theme": "dark" }</c>.
/// Settings are a convenience, so an unreadable or unwritable file never stops the viewer:
/// loading falls back to <see cref="ThemePreference.System"/> and a failed save is ignored.
/// </summary>
public sealed class ThemeStore(string path)
{
    private const string ThemeKey = "theme";

    private readonly SettingsFile _file = new(path);

    public ThemePreference Load() => TryLoad(out var preference) ? preference : ThemePreference.System;

    /// <summary>
    /// Reads the saved preference. A missing, corrupt or unknown setting counts as
    /// <see cref="ThemePreference.System"/>; false means the file could not be read, so what it says is unknown.
    /// </summary>
    public bool TryLoad(out ThemePreference preference)
    {
        preference = ThemePreference.System;
        if (_file.TryRead() is not { } settings)
            return false;

        if (settings[ThemeKey] is { } theme && theme.GetValueKind() == JsonValueKind.String)
        {
            preference = theme.GetValue<string>() switch
            {
                "light" => ThemePreference.Light,
                "dark" => ThemePreference.Dark,
                _ => ThemePreference.System,
            };
        }
        return true;
    }

    public void Save(ThemePreference preference) =>
        _file.Update(settings => settings[ThemeKey] = preference.ToString().ToLowerInvariant());
}
