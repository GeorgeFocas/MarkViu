using Microsoft.Win32;

namespace MarkViu.Theming;

public static class SystemTheme
{
    public const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>Whether Windows is set to dark mode for apps. Windows defaults to light when the value is missing.</summary>
    /// <param name="root">Key standing in for HKEY_CURRENT_USER (tests pass a scratch key).</param>
    public static bool IsDark(RegistryKey? root = null)
    {
        using var key = (root ?? Registry.CurrentUser).OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue(AppsUseLightThemeValue) is int useLight && useLight == 0;
    }
}
