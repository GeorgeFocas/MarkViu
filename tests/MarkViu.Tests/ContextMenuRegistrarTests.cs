using MarkViu.Shell;
using Microsoft.Win32;

namespace MarkViu.Tests;

public sealed class ContextMenuRegistrarTests : IDisposable
{
    private const string Exe = @"C:\Program Files\MarkViu\MarkViu.exe";

    // A scratch key that stands in for HKEY_CURRENT_USER.
    private readonly string _rootPath = $@"Software\MarkViu.Tests\{Guid.NewGuid():N}";
    private readonly RegistryKey _root;

    public ContextMenuRegistrarTests() => _root = Registry.CurrentUser.CreateSubKey(_rootPath);

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\MarkViu.Tests", throwOnMissingSubKey: false);
    }

    [Fact]
    public void Register_writes_verb_for_each_extension()
    {
        var registrar = new ContextMenuRegistrar(_root);
        registrar.Register(Exe);

        foreach (var ext in ContextMenuRegistrar.Extensions)
        {
            using var verb = _root.OpenSubKey(ContextMenuRegistrar.VerbKeyPath(ext));
            Assert.NotNull(verb);
            Assert.Equal("Open in MarkViu", verb.GetValue("MUIVerb"));
            Assert.Equal($"\"{Exe}\",0", verb.GetValue("Icon"));
            using var command = verb.OpenSubKey("command");
            Assert.Equal($"\"{Exe}\" \"%1\"", command!.GetValue(""));
        }
        Assert.True(registrar.IsRegistered(Exe));
        Assert.False(registrar.IsRegistered(@"C:\Other\MarkViu.exe"));
    }

    [Fact]
    public void Unregister_removes_verbs_and_is_idempotent()
    {
        var registrar = new ContextMenuRegistrar(_root);
        registrar.Register(Exe);
        registrar.Unregister();
        registrar.Unregister();

        foreach (var ext in ContextMenuRegistrar.Extensions)
            Assert.Null(_root.OpenSubKey(ContextMenuRegistrar.VerbKeyPath(ext)));
        Assert.False(registrar.IsRegistered(Exe));
    }
}
