using OpcPlc.PluginNodes;
using ReactiveUI;

namespace OpcPlc.Gui.ViewModels.NodeEditor;

public class NodeItem : NodeItemBase
{
    private string _nodeId = string.Empty;
    private string _dataType = "Int32";
    private int _valueRank = -1;
    private string _accessLevel = "CurrentReadOrWrite";
    private string _description = string.Empty;
    private object? _value;
    private SimulationConfig? _simulation;

    public string NodeId
    {
        get => _nodeId;
        set => this.RaiseAndSetIfChanged(ref _nodeId, value);
    }

    public string DataType
    {
        get => _dataType;
        set => this.RaiseAndSetIfChanged(ref _dataType, value);
    }

    public int ValueRank
    {
        get => _valueRank;
        set => this.RaiseAndSetIfChanged(ref _valueRank, value);
    }

    public string AccessLevel
    {
        get => _accessLevel;
        set => this.RaiseAndSetIfChanged(ref _accessLevel, value);
    }

    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public SimulationConfig? Simulation
    {
        get => _simulation;
        set
        {
            this.RaiseAndSetIfChanged(ref _simulation, value);
            this.RaisePropertyChanged(nameof(SimulationEnabled));
        }
    }

    public bool SimulationEnabled
    {
        get => Simulation != null && !string.IsNullOrEmpty(Simulation.Type);
        set
        {
            if (value)
            {
                Simulation ??= new SimulationConfig { Type = "Random", Min = 0, Max = 100 };
                if (string.IsNullOrEmpty(Simulation.Type))
                {
                    Simulation.Type = "Random";
                }
            }
            else
            {
                if (Simulation != null)
                {
                    Simulation.Type = null;
                }
            }
            this.RaisePropertyChanged(nameof(SimulationEnabled));
        }
    }

    public override bool IsFolder => false;

    public static NodeItem FromConfigNode(ConfigNode config)
    {
        return new NodeItem
        {
            Name = config.Name ?? string.Empty,
            NodeId = config.NodeId?.ToString() ?? string.Empty,
            DataType = config.DataType ?? "Int32",
            ValueRank = config.ValueRank,
            AccessLevel = config.AccessLevel ?? "CurrentReadOrWrite",
            Description = config.Description ?? string.Empty,
            Value = config.Value,
            Simulation = config.Simulation == null || string.IsNullOrEmpty(config.Simulation.Type)
                ? null
                : new SimulationConfig
                {
                    Type = config.Simulation.Type,
                    Min = config.Simulation.Min,
                    Max = config.Simulation.Max,
                },
        };
    }

    public ConfigNode ToConfigNode()
    {
        // Try to preserve numeric NodeId when possible for cleaner JSON output.
        dynamic nodeId = NodeId;
        if (long.TryParse(NodeId, out var longId))
        {
            nodeId = longId;
        }

        return new ConfigNode
        {
            Name = Name,
            NodeId = nodeId,
            DataType = DataType,
            ValueRank = ValueRank,
            AccessLevel = AccessLevel,
            Description = Description,
            Value = Value,
            Simulation = Simulation == null || string.IsNullOrEmpty(Simulation.Type)
                ? null
                : new SimulationConfig
                {
                    Type = Simulation.Type,
                    Min = Simulation.Min,
                    Max = Simulation.Max,
                },
        };
    }
}
