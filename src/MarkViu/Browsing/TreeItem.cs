using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkViu.Browsing;

/// <summary>A row of the files panel, bound to a TreeViewItem.</summary>
public sealed class TreeItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    private TreeItem(MarkdownNode node, TreeItem? parent)
    {
        Name = node.Name;
        FullPath = node.FullPath;
        IsDirectory = node.IsDirectory;
        Parent = parent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public TreeItem? Parent { get; }
    public ObservableCollection<TreeItem> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>Builds the rows for a scanned folder, expanding the folders in <paramref name="expandedPaths"/>.</summary>
    public static TreeItem From(MarkdownNode node, ISet<string> expandedPaths) => From(node, null, expandedPaths);

    private static TreeItem From(MarkdownNode node, TreeItem? parent, ISet<string> expandedPaths)
    {
        var item = new TreeItem(node, parent) { _isExpanded = node.IsDirectory && expandedPaths.Contains(node.FullPath) };
        foreach (var child in node.Children)
            item.Children.Add(From(child, item, expandedPaths));
        return item;
    }

    public IEnumerable<TreeItem> Descendants() => Children.SelectMany(c => c.Descendants().Prepend(c));

    public TreeItem? Find(string path) =>
        Descendants().FirstOrDefault(i => string.Equals(i.FullPath, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Expands the folders above this item so it is visible.</summary>
    public void Reveal()
    {
        for (var p = Parent; p is not null; p = p.Parent)
            p.IsExpanded = true;
    }

    private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value)
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
