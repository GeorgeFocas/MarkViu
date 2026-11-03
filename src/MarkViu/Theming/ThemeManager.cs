namespace MarkViu.Theming;

/// <summary>The current theme, shared by every window in the process.</summary>
public sealed class ThemeManager(ThemeStore store, Func<bool> systemIsDark)
{
    /// <summary>Raised when the theme in effect may have changed.</summary>
    public event Action? Changed;

    public ThemePreference Preference { get; private set; } = store.Load();

    public bool IsDark => Preference switch
    {
        ThemePreference.Light => false,
        ThemePreference.Dark => true,
        _ => systemIsDark(),
    };

    /// <summary>Switches to the opposite of the theme in effect and remembers the choice.</summary>
    public void Toggle()
    {
        Preference = IsDark ? ThemePreference.Light : ThemePreference.Dark;
        store.Save(Preference);
        Changed?.Invoke();
    }

    /// <summary>Call when the settings file changes, which is how a toggle in another MarkViu process arrives.</summary>
    public void ReloadPreference()
    {
        if (!store.TryLoad(out var preference) || preference == Preference)
            return;

        Preference = preference;
        Changed?.Invoke();
    }

    /// <summary>Call when the Windows theme may have changed. A saved choice takes priority over it.</summary>
    public void NotifySystemThemeChanged()
    {
        if (Preference == ThemePreference.System)
            Changed?.Invoke();
    }
}
