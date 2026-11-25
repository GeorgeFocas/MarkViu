using MarkViu.Settings;
using MarkViu.Theming;

namespace MarkViu.Tests;

public sealed class SidebarStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MarkViu.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_loads_the_defaults()
    {
        Assert.Equal(new SidebarLayout(true, SidebarStore.DefaultWidth), new SidebarStore(SettingsPath).Load());
    }

    [Fact]
    public void Saved_layout_loads_back()
    {
        new SidebarStore(SettingsPath).Save(new SidebarLayout(false, 321));

        Assert.Equal(new SidebarLayout(false, 321), new SidebarStore(SettingsPath).Load());
    }

    [Theory]
    [InlineData(10, SidebarStore.MinWidth)]
    [InlineData(5000, SidebarStore.MaxWidth)]
    public void Width_is_kept_within_limits(double saved, double expected)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, $$"""{ "sidebarWidth": {{saved}} }""");

        Assert.Equal(expected, new SidebarStore(SettingsPath).Load().Width);
    }

    [Theory]
    [InlineData("not json {")]
    [InlineData("[]")]
    [InlineData("""{ "sidebarVisible": "yes", "sidebarWidth": "wide" }""")]
    public void Corrupt_values_load_as_defaults(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, content);

        Assert.Equal(SidebarStore.Default, new SidebarStore(SettingsPath).Load());
    }

    [Fact]
    public void Theme_and_sidebar_keep_each_others_settings()
    {
        var theme = new ThemeStore(SettingsPath);
        var sidebar = new SidebarStore(SettingsPath);

        theme.Save(ThemePreference.Dark);
        sidebar.Save(new SidebarLayout(false, 300));
        Assert.Equal(ThemePreference.Dark, theme.Load());

        theme.Save(ThemePreference.Light);
        Assert.Equal(new SidebarLayout(false, 300), sidebar.Load());
        Assert.Equal([SettingsPath], Directory.GetFiles(_dir));
    }

    [Fact]
    public void Save_does_not_overwrite_a_file_it_cannot_read()
    {
        new ThemeStore(SettingsPath).Save(ThemePreference.Dark);

        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            new SidebarStore(SettingsPath).Save(new SidebarLayout(false, 300));

        Assert.Equal(ThemePreference.Dark, new ThemeStore(SettingsPath).Load());
        Assert.Equal([SettingsPath], Directory.GetFiles(_dir));
    }
}
