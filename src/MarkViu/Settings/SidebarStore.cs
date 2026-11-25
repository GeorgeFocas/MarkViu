using System.Text.Json.Nodes;

namespace MarkViu.Settings;

public readonly record struct SidebarLayout(bool Visible, double Width);

/// <summary>Remembers whether the files panel is open and how wide it is, for example <c>{ "sidebarVisible": true, "sidebarWidth": 260 }</c>.</summary>
public sealed class SidebarStore(string path)
{
    public const double DefaultWidth = 260;
    public const double MinWidth = 150;
    public const double MaxWidth = 600;

    private const string VisibleKey = "sidebarVisible";
    private const string WidthKey = "sidebarWidth";

    private readonly SettingsFile _file = new(path);

    public static SidebarLayout Default => new(true, DefaultWidth);

    public SidebarLayout Load()
    {
        if (_file.TryRead() is not { } settings)
            return Default;

        bool visible = settings[VisibleKey] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : Default.Visible;
        double width = settings[WidthKey] is JsonValue w && w.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : DefaultWidth;
        return new SidebarLayout(visible, ClampWidth(width));
    }

    public void Save(SidebarLayout layout) => _file.Update(settings =>
    {
        settings[VisibleKey] = layout.Visible;
        settings[WidthKey] = Math.Round(ClampWidth(layout.Width));
    });

    public static double ClampWidth(double width) => Math.Clamp(width, MinWidth, MaxWidth);
}
