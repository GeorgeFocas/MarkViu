using MarkViu.Theming;

namespace MarkViu.Tests;

public sealed class ThemeManagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MarkViu.Tests", Guid.NewGuid().ToString("N"));
    private readonly ThemeStore _store;
    private bool _systemIsDark;

    public ThemeManagerTests() => _store = new ThemeStore(Path.Combine(_dir, "settings.json"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private ThemeManager CreateManager() => new(_store, () => _systemIsDark);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Follows_the_system_until_first_toggle(bool systemIsDark)
    {
        _systemIsDark = systemIsDark;
        var theme = CreateManager();

        Assert.Equal(ThemePreference.System, theme.Preference);
        Assert.Equal(systemIsDark, theme.IsDark);
    }

    [Theory]
    [InlineData(false, ThemePreference.Dark)]
    [InlineData(true, ThemePreference.Light)]
    public void First_toggle_goes_to_the_opposite_of_the_system(bool systemIsDark, ThemePreference expected)
    {
        _systemIsDark = systemIsDark;
        var theme = CreateManager();

        theme.Toggle();

        Assert.Equal(expected, theme.Preference);
        Assert.Equal(!systemIsDark, theme.IsDark);
    }

    [Fact]
    public void Toggling_flips_between_light_and_dark()
    {
        var theme = CreateManager();

        theme.Toggle();
        Assert.Equal(ThemePreference.Dark, theme.Preference);
        theme.Toggle();
        Assert.Equal(ThemePreference.Light, theme.Preference);
        theme.Toggle();
        Assert.Equal(ThemePreference.Dark, theme.Preference);
    }

    [Fact]
    public void Toggle_saves_the_choice_and_raises_changed()
    {
        var theme = CreateManager();
        int changed = 0;
        theme.Changed += () => changed++;

        theme.Toggle();

        Assert.Equal(1, changed);
        Assert.Equal(ThemePreference.Dark, _store.Load());
        Assert.Equal(ThemePreference.Dark, CreateManager().Preference);
    }

    [Fact]
    public void Saved_choice_ignores_the_system_theme()
    {
        _store.Save(ThemePreference.Light);
        _systemIsDark = true;

        Assert.False(CreateManager().IsDark);
    }

    [Fact]
    public void Toggle_in_another_process_is_picked_up_on_reload()
    {
        var mine = CreateManager();
        var other = CreateManager();
        int changed = 0;
        mine.Changed += () => changed++;

        other.Toggle();
        mine.ReloadPreference();

        Assert.Equal(1, changed);
        Assert.Equal(ThemePreference.Dark, mine.Preference);
    }

    [Fact]
    public void Reloading_own_save_does_not_raise_changed()
    {
        var theme = CreateManager();
        theme.Toggle();
        int changed = 0;
        theme.Changed += () => changed++;

        theme.ReloadPreference();

        Assert.Equal(0, changed);
        Assert.Equal(ThemePreference.Dark, theme.Preference);
    }

    [Fact]
    public void Unreadable_settings_keep_the_current_theme()
    {
        var theme = CreateManager();
        theme.Toggle();
        int changed = 0;
        theme.Changed += () => changed++;

        using var locked = new FileStream(Path.Combine(_dir, "settings.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        theme.ReloadPreference();

        Assert.Equal(0, changed);
        Assert.Equal(ThemePreference.Dark, theme.Preference);
    }

    [Fact]
    public void System_change_raises_changed_only_while_following_the_system()
    {
        var theme = CreateManager();
        int changed = 0;
        theme.Changed += () => changed++;

        theme.NotifySystemThemeChanged();
        Assert.Equal(1, changed);

        theme.Toggle();
        changed = 0;
        theme.NotifySystemThemeChanged();
        Assert.Equal(0, changed);
    }
}
