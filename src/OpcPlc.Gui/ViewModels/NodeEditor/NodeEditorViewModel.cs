using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Windows.Input;

namespace OpcPlc.Gui.ViewModels.NodeEditor;

public class NodeEditorViewModel : ReactiveObject
{
    private NodeItemBase? _selectedItem;
    private FolderItem _root = new();

    public FolderItem Root
    {
        get => _root;
        set => this.RaiseAndSetIfChanged(ref _root, value);
    }

    public NodeItemBase? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    private bool _isReadOnly;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> AddNodeCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public NodeEditorViewModel()
    {
        AddFolderCommand = ReactiveCommand.Create(() =>
        {
            var target = GetAddTarget();
            var newFolder = new FolderItem { Name = "NewFolder" };
            target.Children.Add(newFolder);
            SelectedItem = newFolder;
        });

        AddNodeCommand = ReactiveCommand.Create(() =>
        {
            var target = GetAddTarget();
            var newNode = new NodeItem { NodeId = "NewNode", Name = "NewNode" };
            target.Children.Add(newNode);
            SelectedItem = newNode;
        });

        DeleteCommand = ReactiveCommand.Create(() =>
        {
            var item = SelectedItem;
            if (item == null || item == Root)
            {
                return;
            }

            var parent = FindParent(Root, item);
            if (parent != null)
            {
                parent.Children.Remove(item);
                SelectedItem = null;
            }
        });
    }

    private FolderItem GetAddTarget()
    {
        if (SelectedItem is FolderItem folder)
        {
            return folder;
        }

        if (SelectedItem is NodeItem node)
        {
            var parent = FindParent(Root, node);
            if (parent != null)
            {
                return parent;
            }
        }

        return Root;
    }

    private static FolderItem? FindParent(FolderItem root, NodeItemBase target)
    {
        foreach (var child in root.Children)
        {
            if (child == target)
            {
                return root;
            }

            if (child is FolderItem childFolder)
            {
                var result = FindParent(childFolder, target);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }
}
