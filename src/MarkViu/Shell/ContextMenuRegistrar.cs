using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MarkViu.Shell;

/// <summary>
/// Adds "Open in MarkViu" to the Explorer context menu of markdown files, per user (no admin needed).
/// Uses SystemFileAssociations so the user's default .md app is left untouched.
/// </summary>
public sealed partial class ContextMenuRegistrar
{
    public const string MenuText = "Open in MarkViu";
    public static readonly string[] Extensions = [".md", ".markdown"];

    private readonly RegistryKey _root;

    /// <param name="root">Key standing in for HKEY_CURRENT_USER (tests pass a scratch key).</param>
    public ContextMenuRegistrar(RegistryKey? root = null) => _root = root ?? Registry.CurrentUser;

    public static string VerbKeyPath(string extension) => $@"Software\Classes\SystemFileAssociations\{extension}\shell\MarkViu";

    public void Register(string exePath)
    {
        foreach (var ext in Extensions)
        {
            using var verb = _root.CreateSubKey(VerbKeyPath(ext));
            verb.SetValue("", MenuText);
            verb.SetValue("MUIVerb", MenuText);
            verb.SetValue("Icon", $"\"{exePath}\",0");
            using var command = verb.CreateSubKey("command");
            command.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        NotifyShell();
    }

    public void Unregister()
    {
        foreach (var ext in Extensions)
            _root.DeleteSubKeyTree(VerbKeyPath(ext), throwOnMissingSubKey: false);
        NotifyShell();
    }

    public bool IsRegistered(string exePath)
    {
        using var command = _root.OpenSubKey(VerbKeyPath(Extensions[0]) + @"\command");
        return command?.GetValue("") is string value && value.StartsWith($"\"{exePath}\"", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyShell()
    {
        if (_root.Name == Registry.CurrentUser.Name)
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
