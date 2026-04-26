using Newtonsoft.Json;
using OpcPlc.Gui.ViewModels.NodeEditor;
using OpcPlc.PluginNodes;
using System.ComponentModel;
using Xunit;

namespace OpcPlc.Gui.Tests.NodeEditor;

public class NodeItemTests
{
    [Fact]
    public void PropertyChanged_Fires_On_Name_Change()
    {
        var item = new NodeItem { Name = "Old" };
        var fired = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeItem.Name))
            {
                fired = true;
            }
        };

        item.Name = "New";

        Assert.True(fired);
    }

    [Fact]
    public void PropertyChanged_Fires_On_NodeId_Change()
    {
        var item = new NodeItem();
        var fired = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeItem.NodeId))
            {
                fired = true;
            }
        };

        item.NodeId = "1023";

        Assert.True(fired);
    }

    [Fact]
    public void RoundTrip_Preserves_String_NodeId()
    {
        var original = new ConfigNode
        {
            NodeId = "aRMS",
            Name = "aRMS",
            DataType = "Int32",
            ValueRank = -1,
            AccessLevel = "CurrentRead",
            Description = "Test",
        };

        var item = NodeItem.FromConfigNode(original);
        var result = item.ToConfigNode();

        Assert.Equal("aRMS", result.NodeId?.ToString());
        Assert.Equal("aRMS", result.Name);
    }

    [Fact]
    public void RoundTrip_Preserves_Numeric_NodeId_As_Number()
    {
        var original = new ConfigNode
        {
            NodeId = 1023L,
            Name = "ActualSpeed",
            DataType = "Float",
            ValueRank = 1,
            AccessLevel = "CurrentReadOrWrite",
            Description = "Rotational speed",
        };

        var item = NodeItem.FromConfigNode(original);
        var result = item.ToConfigNode();

        Assert.IsType<long>(result.NodeId);
        Assert.Equal(1023L, result.NodeId);
    }

    [Fact]
    public void Json_Serialize_Deserialize_RoundTrip()
    {
        var root = new FolderItem { Name = "Root" };
        root.Children.Add(new FolderItem { Name = "ChildFolder" });
        root.Children.Add(new NodeItem
        {
            NodeId = "1048",
            Name = "Int32 array",
            DataType = "Int32",
            ValueRank = 1,
            AccessLevel = "CurrentReadOrWrite",
            Description = "Test array",
            Value = new[] { 1, 2, 3, 4, 5 },
        });

        var config = root.ToConfigFolder();
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<ConfigFolder>(json);
        var restored = FolderItem.FromConfigFolder(deserialized!);

        Assert.Equal("Root", restored.Name);
        Assert.Equal(2, restored.Children.Count);
        Assert.IsType<FolderItem>(restored.Children[0]);
        var node = Assert.IsType<NodeItem>(restored.Children[1]);
        Assert.Equal("Int32 array", node.Name);
        Assert.Equal("1048", node.NodeId);
    }

    [Fact]
    public void FolderItem_IsFolder_True()
    {
        Assert.True(new FolderItem().IsFolder);
    }

    [Fact]
    public void NodeItem_IsFolder_False()
    {
        Assert.False(new NodeItem().IsFolder);
    }

    [Fact]
    public void RoundTrip_Preserves_SimulationConfig()
    {
        var original = new ConfigNode
        {
            NodeId = "1023",
            Name = "ActualSpeed",
            DataType = "Float",
            Simulation = new SimulationConfig
            {
                Type = "Random",
                Min = 10,
                Max = 50,
            },
        };

        var item = NodeItem.FromConfigNode(original);
        Assert.True(item.SimulationEnabled);
        Assert.Equal("Random", item.Simulation?.Type);
        Assert.Equal(10, item.Simulation?.Min);
        Assert.Equal(50, item.Simulation?.Max);

        var result = item.ToConfigNode();
        Assert.NotNull(result.Simulation);
        Assert.Equal("Random", result.Simulation.Type);
        Assert.Equal(10, result.Simulation.Min);
        Assert.Equal(50, result.Simulation.Max);
    }

    [Fact]
    public void RoundTrip_Null_Simulation_Remains_Null()
    {
        var original = new ConfigNode
        {
            NodeId = "1023",
            Name = "ActualSpeed",
        };

        var item = NodeItem.FromConfigNode(original);
        Assert.False(item.SimulationEnabled);
        Assert.Null(item.Simulation);

        var result = item.ToConfigNode();
        Assert.Null(result.Simulation);
    }

    [Fact]
    public void SimulationEnabled_SetTrue_Creates_Default_Random_Config()
    {
        var item = new NodeItem();
        Assert.False(item.SimulationEnabled);

        item.SimulationEnabled = true;

        Assert.True(item.SimulationEnabled);
        Assert.NotNull(item.Simulation);
        Assert.Equal("Random", item.Simulation.Type);
    }

    [Fact]
    public void RoundTrip_Preserves_Sine_SimulationConfig()
    {
        var original = new ConfigNode
        {
            NodeId = "Fermenter.F11.Temperature.PV",
            Name = "PV",
            DataType = "Float",
            Simulation = new SimulationConfig
            {
                Type = "Sine",
                Min = 0,
                Max = 100,
                Base = 36.5,
                Amplitude = 1.5,
                PeriodSeconds = 60,
            },
        };

        var item = NodeItem.FromConfigNode(original);
        Assert.True(item.SimulationEnabled);
        Assert.Equal("Sine", item.Simulation?.Type);
        Assert.Equal(36.5, item.Simulation?.Base);
        Assert.Equal(1.5, item.Simulation?.Amplitude);
        Assert.Equal(60, item.Simulation?.PeriodSeconds);

        var result = item.ToConfigNode();
        Assert.NotNull(result.Simulation);
        Assert.Equal("Sine", result.Simulation.Type);
        Assert.Equal(36.5, result.Simulation.Base);
        Assert.Equal(1.5, result.Simulation.Amplitude);
        Assert.Equal(60, result.Simulation.PeriodSeconds);
    }

    [Fact]
    public void SimulationEnabled_SetTrue_Creates_Default_With_All_Fields()
    {
        var item = new NodeItem();
        Assert.False(item.SimulationEnabled);

        item.SimulationEnabled = true;

        Assert.True(item.SimulationEnabled);
        Assert.NotNull(item.Simulation);
        Assert.Equal("Random", item.Simulation.Type);
        Assert.Equal(0, item.Simulation.Min);
        Assert.Equal(100, item.Simulation.Max);
        Assert.Equal(0, item.Simulation.Base);
        Assert.Equal(1, item.Simulation.Amplitude);
        Assert.Equal(10, item.Simulation.PeriodSeconds);
        Assert.Equal(1, item.Simulation.StepPerSecond);
        Assert.NotNull(item.Simulation.Values);
        Assert.Empty(item.Simulation.Values);
        Assert.Equal(1, item.Simulation.IntervalSeconds);
    }

    [Fact]
    public void Simulation_Type_Can_Be_Changed_To_Sine()
    {
        var item = new NodeItem();
        item.SimulationEnabled = true;
        item.Simulation!.Type = "Sine";

        Assert.Equal("Sine", item.Simulation.Type);
        Assert.True(item.SimulationEnabled);
    }
}
