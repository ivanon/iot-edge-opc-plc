using OpcPlc.Gui.ViewModels.NodeEditor;
using System.Windows.Input;
using Xunit;

namespace OpcPlc.Gui.Tests.NodeEditor;

public class NodeEditorViewModelTests
{
    [Fact]
    public void AddFolder_WhenFolderSelected_AddsChildFolder()
    {
        var vm = new NodeEditorViewModel();
        var root = new FolderItem { Name = "Root" };
        var child = new FolderItem { Name = "Child" };
        root.Children.Add(child);
        vm.Root = root;
        vm.SelectedItem = child;

        ((ICommand)vm.AddFolderCommand).Execute(null);

        Assert.Single(child.Children);
        Assert.IsType<FolderItem>(child.Children[0]);
        Assert.Equal("NewFolder", child.Children[0].Name);
        Assert.Equal(child.Children[0], vm.SelectedItem);
    }

    [Fact]
    public void AddNode_WhenNodeSelected_AddsSiblingToParent()
    {
        var vm = new NodeEditorViewModel();
        var root = new FolderItem { Name = "Root" };
        var node = new NodeItem { NodeId = "1", Name = "A" };
        root.Children.Add(node);
        vm.Root = root;
        vm.SelectedItem = node;

        ((ICommand)vm.AddNodeCommand).Execute(null);

        Assert.Equal(2, root.Children.Count);
        Assert.IsType<NodeItem>(root.Children[1]);
        Assert.Equal("NewNode", root.Children[1].Name);
        Assert.Equal(root.Children[1], vm.SelectedItem);
    }

    [Fact]
    public void Delete_WhenNodeSelected_RemovesNode()
    {
        var vm = new NodeEditorViewModel();
        var root = new FolderItem { Name = "Root" };
        var node = new NodeItem { NodeId = "1", Name = "A" };
        root.Children.Add(node);
        vm.Root = root;
        vm.SelectedItem = node;

        ((ICommand)vm.DeleteCommand).Execute(null);

        Assert.Empty(root.Children);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public void Delete_Root_IsIgnored()
    {
        var vm = new NodeEditorViewModel();
        var root = new FolderItem { Name = "Root" };
        vm.Root = root;
        vm.SelectedItem = root;

        ((ICommand)vm.DeleteCommand).Execute(null);

        Assert.Equal("Root", vm.Root.Name);
        Assert.Equal(root, vm.SelectedItem);
    }

    [Fact]
    public void IsReadOnly_CanBeSet()
    {
        var vm = new NodeEditorViewModel();
        Assert.False(vm.IsReadOnly);

        vm.IsReadOnly = true;
        Assert.True(vm.IsReadOnly);
    }
}
