using OpcPlc.Gui.Services;
using OpcPlc.Gui.ViewModels.NodeEditor;
using OpcPlc.PluginNodes;
using System.Collections.Generic;
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

    [Fact]
    public void SaveAndLoad_Preserves_All_Simulation_Modes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nodes-sim-{System.Guid.NewGuid()}.json");
        var service = new NodesFileService(path);

        var root = new FolderItem { Name = "TestRoot" };
        root.Children.Add(new NodeItem
        {
            NodeId = "SineNode",
            Name = "SineNode",
            DataType = "Float",
            Simulation = new SimulationConfig
            {
                Type = "Sine",
                Base = 36.5,
                Amplitude = 1.5,
                PeriodSeconds = 60,
            },
        });
        root.Children.Add(new NodeItem
        {
            NodeId = "RampNode",
            Name = "RampNode",
            DataType = "Float",
            Simulation = new SimulationConfig
            {
                Type = "Ramp",
                Min = 0,
                Max = 10,
                StepPerSecond = 2,
            },
        });
        root.Children.Add(new NodeItem
        {
            NodeId = "StepNode",
            Name = "StepNode",
            DataType = "Float",
            Simulation = new SimulationConfig
            {
                Type = "Step",
                Values = new List<double> { 10, 20, 30 },
                IntervalSeconds = 2,
            },
        });

        service.Save(root);
        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Children.Count);

        var sineNode = Assert.IsType<NodeItem>(loaded.Children[0]);
        Assert.Equal("Sine", sineNode.Simulation?.Type);
        Assert.Equal(36.5, sineNode.Simulation?.Base);
        Assert.Equal(1.5, sineNode.Simulation?.Amplitude);
        Assert.Equal(60, sineNode.Simulation?.PeriodSeconds);

        var rampNode = Assert.IsType<NodeItem>(loaded.Children[1]);
        Assert.Equal("Ramp", rampNode.Simulation?.Type);
        Assert.Equal(0, rampNode.Simulation?.Min);
        Assert.Equal(10, rampNode.Simulation?.Max);
        Assert.Equal(2, rampNode.Simulation?.StepPerSecond);

        var stepNode = Assert.IsType<NodeItem>(loaded.Children[2]);
        Assert.Equal("Step", stepNode.Simulation?.Type);
        Assert.Equal(new List<double> { 10, 20, 30 }, stepNode.Simulation?.Values);
        Assert.Equal(2, stepNode.Simulation?.IntervalSeconds);

        try { File.Delete(path); } catch { }
    }
}
