using MarkViu.Theming;
using Microsoft.Win32;

namespace MarkViu.Tests;

public sealed class SystemThemeTests : IDisposable
{
    // Separate from ContextMenuRegistrarTests, whose cleanup deletes its whole parent key
    // and could run at the same time as these tests.
    private const string ParentPath = @"Software\MarkViu.ThemeTests";

    // A scratch key that stands in for HKEY_CURRENT_USER.
    private readonly RegistryKey _root = Registry.CurrentUser.CreateSubKey($@"{ParentPath}\{Guid.NewGuid():N}");

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(ParentPath, throwOnMissingSubKey: false);
    }

    private void SetAppsUseLightTheme(int value)
    {
        using var key = _root.CreateSubKey(SystemTheme.PersonalizeKeyPath);
        key.SetValue(SystemTheme.AppsUseLightThemeValue, value, RegistryValueKind.DWord);
    }

    [Fact]
    public void Zero_means_dark()
    {
        SetAppsUseLightTheme(0);

        Assert.True(SystemTheme.IsDark(_root));
    }

    [Fact]
    public void One_means_light()
    {
        SetAppsUseLightTheme(1);

        Assert.False(SystemTheme.IsDark(_root));
    }

    [Fact]
    public void Missing_key_means_light()
    {
        Assert.False(SystemTheme.IsDark(_root));
    }

    [Fact]
    public void Missing_value_means_light()
    {
        using var key = _root.CreateSubKey(SystemTheme.PersonalizeKeyPath);

        Assert.False(SystemTheme.IsDark(_root));
    }
}
