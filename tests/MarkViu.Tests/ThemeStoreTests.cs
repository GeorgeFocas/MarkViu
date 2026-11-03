using MarkViu.Theming;

namespace MarkViu.Tests;

public sealed class ThemeStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MarkViu.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.System)]
    public void Saved_preference_loads_back_and_creates_the_folder(ThemePreference preference)
    {
        new ThemeStore(SettingsPath).Save(preference);

        Assert.Equal(preference, new ThemeStore(SettingsPath).Load());
    }

    [Fact]
    public void Missing_file_loads_as_system()
    {
        Assert.Equal(ThemePreference.System, new ThemeStore(SettingsPath).Load());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json {")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "theme": 1 }""")]
    [InlineData("""{ "theme": "sepia" }""")]
    public void Corrupt_or_unknown_content_loads_as_system(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, content);

        Assert.Equal(ThemePreference.System, new ThemeStore(SettingsPath).Load());
    }

    [Fact]
    public void Save_replaces_the_file_and_leaves_no_temp_files()
    {
        var store = new ThemeStore(SettingsPath);

        store.Save(ThemePreference.Dark);
        store.Save(ThemePreference.Light);

        Assert.Equal(ThemePreference.Light, store.Load());
        Assert.Equal([SettingsPath], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Save_waits_for_a_brief_read_by_another_process()
    {
        var store = new ThemeStore(SettingsPath);
        store.Save(ThemePreference.Dark);

        var reader = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var released = Task.Delay(30).ContinueWith(_ => reader.Dispose());
        store.Save(ThemePreference.Light);
        await released;

        Assert.Equal(ThemePreference.Light, store.Load());
    }

    [Fact]
    public void Save_gives_up_quietly_when_the_file_stays_locked()
    {
        var store = new ThemeStore(SettingsPath);
        store.Save(ThemePreference.Dark);

        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            store.Save(ThemePreference.Light);

        Assert.Equal(ThemePreference.Dark, store.Load());
        Assert.Equal([SettingsPath], Directory.GetFiles(_dir));
    }

    [Fact]
    public void TryLoad_treats_a_missing_file_as_system()
    {
        Assert.True(new ThemeStore(SettingsPath).TryLoad(out var preference));
        Assert.Equal(ThemePreference.System, preference);
    }

    [Fact]
    public void TryLoad_reports_a_file_it_cannot_read()
    {
        var store = new ThemeStore(SettingsPath);
        store.Save(ThemePreference.Dark);

        using var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(store.TryLoad(out _));
    }

    [Fact]
    public void Failed_save_does_not_throw()
    {
        // A file where the settings folder should be makes the save fail.
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");
        var store = new ThemeStore(Path.Combine(blocker, "settings.json"));

        store.Save(ThemePreference.Dark);

        Assert.Equal(ThemePreference.System, store.Load());
    }
}
