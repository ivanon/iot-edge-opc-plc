using OpcPlc.Gui.Services;
using OpcPlc.Gui.ViewModels.NodeEditor;
using System.IO;
using Xunit;

namespace OpcPlc.Gui.Tests.Services;

public class NodesFileServiceTests
{
    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var service = new NodesFileService(
            Path.Combine(Path.GetTempPath(), $"missing-{System.Guid.NewGuid()}.json"));

        var result = service.Load();

        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesHierarchy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nodes-{System.Guid.NewGuid()}.json");
        var service = new NodesFileService(path);

        var root = new FolderItem { Name = "MyTelemetry" };
        var childFolder = new FolderItem { Name = "Child" };
        childFolder.Children.Add(new NodeItem
        {
            NodeId = "9999",
            Name = "Child Node",
            Description = "Child Node for testing",
        });
        root.Children.Add(childFolder);
        root.Children.Add(new NodeItem
        {
            NodeId = "1023",
            Name = "ActualSpeed",
            DataType = "Float",
            ValueRank = -1,
            AccessLevel = "CurrentReadOrWrite",
            Description = "Rotational speed",
        });

        service.Save(root);
        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal("MyTelemetry", loaded!.Name);
        Assert.Equal(2, loaded.Children.Count);
        Assert.IsType<FolderItem>(loaded.Children[0]);
        Assert.IsType<NodeItem>(loaded.Children[1]);

        var node = (NodeItem)loaded.Children[1];
        Assert.Equal("1023", node.NodeId);
        Assert.Equal("Float", node.DataType);

        try { File.Delete(path); } catch { }
    }
}
