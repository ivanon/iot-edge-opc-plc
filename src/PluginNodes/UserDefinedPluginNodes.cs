namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Nodes that are configured via JSON file.
/// </summary>
public partial class UserDefinedPluginNodes(TimeService timeService, ILogger logger) : PluginNodeBase(timeService, logger), IPluginNodes
{
    private string _nodesFileName;
    private PlcNodeManager _plcNodeManager;
    private readonly List<(BaseDataVariableState Variable, ConfigNode Config)> _simulatedVariables = new();
    private readonly List<ITimer> _simulationTimers = new();
    private readonly Random _random = new();
    private readonly Dictionary<NodeId, BaseDataVariableState> _variablesByNodeId = new();
    private readonly Dictionary<NodeId, SimulationState> _simulationStates = new();

    private class SimulationState
    {
        public double ElapsedSeconds;
        public double CurrentRamp;
        public int StepIndex;
        public bool RampDirectionUp = true;
    }

    public void AddOptions(Mono.Options.OptionSet optionSet)
    {
        optionSet.Add(
            "nf|nodesfile=",
            "the filename that contains the list of nodes to be created in the OPC UA address space.",
            (string s) => _nodesFileName = s);
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        if (!string.IsNullOrEmpty(_nodesFileName))
        {
            AddNodes((FolderState)telemetryFolder.Parent); // Root.
        }
    }

    public void StartSimulation()
    {
        if (_plcNodeManager?.PlcSimulationInstance == null)
        {
            return;
        }

        int periodMs = 1000; // 统一 1Hz

        foreach (var (variable, config) in _simulatedVariables)
        {
            var timer = _timeService.NewTimer((s, e) =>
            {
                UpdateSimulatedValue(variable, config);
            }, (uint)periodMs);

            _simulationTimers.Add(timer);
        }
    }

    public void StopSimulation()
    {
        foreach (var timer in _simulationTimers)
        {
            timer.Enabled = false;
        }

        _simulationTimers.Clear();
        _simulationStates.Clear();
    }

    private void UpdateSimulatedValue(BaseDataVariableState variable, ConfigNode config)
    {
        var sim = config.Simulation;
        if (sim == null || string.IsNullOrEmpty(sim.Type))
        {
            return;
        }

        var nodeId = variable.NodeId;
        if (!_simulationStates.TryGetValue(nodeId, out var state))
        {
            state = new SimulationState
            {
                CurrentRamp = sim.Min,
            };
            _simulationStates[nodeId] = state;
        }

        double value;

        switch (sim.Type)
        {
            case "Random":
                value = sim.Min + _random.NextDouble() * (sim.Max - sim.Min);
                break;

            case "Sine":
                {
                    double angle = 2 * Math.PI * state.ElapsedSeconds / sim.PeriodSeconds;
                    value = sim.Base + sim.Amplitude * Math.Sin(angle);
                    state.ElapsedSeconds += 1.0;
                }
                break;

            case "Ramp":
                {
                    if (state.RampDirectionUp)
                    {
                        state.CurrentRamp += sim.StepPerSecond;
                        if (state.CurrentRamp > sim.Max)
                        {
                            state.CurrentRamp = sim.Max;
                            state.RampDirectionUp = false;
                        }
                    }
                    else
                    {
                        state.CurrentRamp -= sim.StepPerSecond;
                        if (state.CurrentRamp < sim.Min)
                        {
                            state.CurrentRamp = sim.Min;
                            state.RampDirectionUp = true;
                        }
                    }
                    value = state.CurrentRamp;
                }
                break;

            case "Step":
                {
                    if (sim.Values == null || sim.Values.Count == 0)
                    {
                        return;
                    }

                    state.ElapsedSeconds += 1.0;
                    if (state.ElapsedSeconds >= sim.IntervalSeconds)
                    {
                        state.ElapsedSeconds = 0;
                        state.StepIndex = (state.StepIndex + 1) % sim.Values.Count;
                    }
                    value = sim.Values[state.StepIndex];
                }
                break;

            default:
                return;
        }

        object typedValue = config.DataType switch
        {
            "Boolean" => _random.NextDouble() >= 0.5,
            "Float" => (float)value,
            "Double" => value,
            "UInt32" => (uint)Math.Clamp(value, uint.MinValue, uint.MaxValue),
            "Int32" => (int)Math.Clamp(value, int.MinValue, int.MaxValue),
            "UInt16" => (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue),
            "Int16" => (short)Math.Clamp(value, short.MinValue, short.MaxValue),
            "Byte" => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue),
            "SByte" => (sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue),
            "UInt64" => (ulong)Math.Clamp(value, 0, long.MaxValue),
            "Int64" => (long)Math.Clamp(value, long.MinValue, long.MaxValue),
            _ => value,
        };

        variable.Value = typedValue;
        variable.Timestamp = _timeService.Now();
        variable.ClearChangeMasks(_plcNodeManager.SystemContext, false);
    }

    private void AddNodes(FolderState folder)
    {
        try
        {
            string json = File.ReadAllText(_nodesFileName);

            var cfgFolder = JsonConvert.DeserializeObject<ConfigFolder>(json, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None,
            });

            LogProcessingNodeInformation(_nodesFileName);

            Nodes = AddNodes(folder, cfgFolder).ToList();
        }
        catch (Exception e)
        {
            LogErrorLoadingUserDefinedNodeFile(e, _nodesFileName, e.Message);
        }


        LogCompletedProcessingUserDefinedNodeFile();
    }

    private IEnumerable<NodeWithIntervals> AddNodes(FolderState folder, ConfigFolder cfgFolder, string pathPrefix = "")
    {
        string folderPath = string.IsNullOrEmpty(pathPrefix) ? cfgFolder.Folder : $"{pathPrefix}.{cfgFolder.Folder}";
        LogCreateFolder(folderPath);
        FolderState userNodesFolder = _plcNodeManager.CreateFolder(
            folder,
            path: folderPath,
            name: cfgFolder.Folder,
            NamespaceType.OpcPlcApplications);

        foreach (var node in cfgFolder.NodeList)
        {
            bool isDecimal = node.NodeId is long;
            bool isString = node.NodeId is string;

            if (!isDecimal && !isString)
            {
                LogUnsupportedNodeType(node.Name, node.NodeId.GetType().ToString());
                node.NodeId = node.NodeId.ToString();
            }

            bool isGuid = false;
            if (Guid.TryParse(node.NodeId.ToString(), out Guid guidNodeId))
            {
                isGuid = true;
                node.NodeId = guidNodeId;
            }

            string typedNodeId = isDecimal
                ? $"i={node.NodeId.ToString()}"
                : isGuid
                    ? $"g={node.NodeId.ToString()}"
                    : $"s={node.NodeId.ToString()}";

            if (node.ValueRank == 1 && node.Value is JArray jArrayValue)
            {
                node.Value = UpdateArrayValue(node, jArrayValue);
            }

            if (string.IsNullOrEmpty(node.Name))
            {
                node.Name = typedNodeId;
            }

            if (string.IsNullOrEmpty(node.Description))
            {
                node.Description = node.Name;
            }

            LogCreateNode(typedNodeId, node.Name, (string)node.NodeId.GetType().Name, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.OpcPlcApplications]);

            var nodeId = isString
                ? new NodeId(node.NodeId, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.OpcPlcApplications])
                : (NodeId)node.NodeId;

            if (node.Method != null)
            {
                CreateMethod(userNodesFolder, node, nodeId);
            }
            else
            {
                var variable = CreateBaseVariable(userNodesFolder, node);

                if (node.Simulation != null && !string.IsNullOrEmpty(node.Simulation.Type))
                {
                    _simulatedVariables.Add((variable, node));
                }

                _variablesByNodeId[nodeId] = variable;
            }

            yield return PluginNodesHelper.GetNodeWithIntervals(nodeId, _plcNodeManager);
        }

        foreach (var childNode in AddFolders(userNodesFolder, cfgFolder, folderPath))
        {
            yield return childNode;
        }
    }

    private IEnumerable<NodeWithIntervals> AddFolders(FolderState folder, ConfigFolder cfgFolder, string pathPrefix)
    {
        if (cfgFolder.FolderList is null)
        {
            yield break;
        }

        foreach (var childFolder in cfgFolder.FolderList)
        {
            foreach (var node in AddNodes(folder, childFolder, pathPrefix))
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    public BaseDataVariableState CreateBaseVariable(NodeState parent, ConfigNode node)
    {
        if (!Enum.TryParse(node.DataType, out BuiltInType nodeDataType))
        {
            LogCannotParseDataType(node.DataType, node.NodeId.ToString());
            node.DataType = "Int32";
        }

        // We have to hard code the conversion here, because AccessLevel is defined as byte in OPC UA lib.
        byte accessLevel;
        try
        {
            accessLevel = (byte)(typeof(AccessLevels).GetField(node.AccessLevel).GetValue(null));
        }
        catch
        {
            LogUnsupportedAccessLevel(node.AccessLevel, node.Name);
            node.AccessLevel = "CurrentReadOrWrite";
            accessLevel = AccessLevels.CurrentReadOrWrite;
        }

        return _plcNodeManager.CreateBaseVariable(parent, node.NodeId, node.Name, new NodeId((uint)nodeDataType), node.ValueRank, accessLevel, node.Description, NamespaceType.OpcPlcApplications, node?.Value);
    }

    private static object UpdateArrayValue(ConfigNode node, JArray jArrayValue)
    {
        return node.DataType switch {
            "String" => jArrayValue.ToObject<string[]>(),
            "Boolean" => jArrayValue.ToObject<bool[]>(),
            "Float" => jArrayValue.ToObject<float[]>(),
            "UInt32" => jArrayValue.ToObject<uint[]>(),
            "Int32" => jArrayValue.ToObject<int[]>(),
            _ => throw new NotImplementedException($"Node type not implemented: {node.DataType}."),
        };
    }

    private void CreateMethod(FolderState parent, ConfigNode node, NodeId nodeId)
    {
        MethodState method = _plcNodeManager.CreateMethod(parent, (string)node.NodeId.ToString()!, node.Name, node.Description, NamespaceType.OpcPlcApplications);

        ushort namespaceIndex = _plcNodeManager.NamespaceIndexes[(int)NamespaceType.OpcPlcApplications];

        method.InputArguments = new PropertyState<Argument[]>(method)
        {
            NodeId = new NodeId(nodeId.Identifier + "_InputArguments", namespaceIndex),
            BrowseName = BrowseNames.InputArguments,
            DisplayName = new LocalizedText(BrowseNames.InputArguments),
            TypeDefinitionId = VariableTypeIds.PropertyType,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            DataType = DataTypeIds.Argument,
            ValueRank = ValueRanks.OneDimension,
            ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 }),
            Value = new[]
            {
                new Argument { Name = "fermenterName", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar },
                new Argument { Name = "targetTemp", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
            },
            StatusCode = StatusCodes.Good,
        };

        method.OutputArguments = new PropertyState<Argument[]>(method)
        {
            NodeId = new NodeId(nodeId.Identifier + "_OutputArguments", namespaceIndex),
            BrowseName = BrowseNames.OutputArguments,
            DisplayName = new LocalizedText(BrowseNames.OutputArguments),
            TypeDefinitionId = VariableTypeIds.PropertyType,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            DataType = DataTypeIds.Argument,
            ValueRank = ValueRanks.OneDimension,
            ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 }),
            Value = new[]
            {
                new Argument { Name = "success", DataType = DataTypeIds.Boolean, ValueRank = ValueRanks.Scalar },
            },
            StatusCode = StatusCodes.Good,
        };

        method.AddChild(method.InputArguments);
        method.AddChild(method.OutputArguments);

        method.OnCallMethod2 = (ISystemContext context, MethodState methodHandle, NodeId objectId, IList<object> inputArguments, IList<object> outputArguments) =>
        {
            string fermenterName = (string)inputArguments[0];
            double targetTemp = (double)inputArguments[1];

            string nodeIdString = $"Fermenter.{fermenterName}.Temperature.SP";
            var targetNodeId = new NodeId(nodeIdString, namespaceIndex);

            if (_variablesByNodeId.TryGetValue(targetNodeId, out var variable))
            {
                variable.Value = targetTemp;
                variable.Timestamp = _timeService.Now();
                variable.ClearChangeMasks(context, false);
            }
            else
            {
                LogMethodTargetNodeNotFound(nodeIdString);
            }

            outputArguments[0] = true;
            return ServiceResult.Good;
        };

        _plcNodeManager.AddPredefinedNode(method);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing node information configured in {NodesFileName}")]
    partial void LogProcessingNodeInformation(string nodesFileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error loading user defined node file {File}: {Error}")]
    partial void LogErrorLoadingUserDefinedNodeFile(Exception exception, string file, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed processing user defined node file")]
    partial void LogCompletedProcessingUserDefinedNodeFile();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Create folder {Folder}")]
    partial void LogCreateFolder(string folder);

    [LoggerMessage(Level = LogLevel.Error, Message = "The type of the node configuration for node with name {Name} ({NodeIdType}) is not supported. Only decimal, string, and GUID are supported. Defaulting to string.")]
    partial void LogUnsupportedNodeType(string name, string nodeIdType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Create node with Id {TypedNodeId}, BrowseName {Name} and type {Type} in namespace with index {NamespaceIndex}")]
    partial void LogCreateNode(string typedNodeId, string name, string type, ushort namespaceIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Value {DataType} of node {NodeId} cannot be parsed. Defaulting to Int32")]
    partial void LogCannotParseDataType(string dataType, string nodeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "AccessLevel {AccessLevel} of node {Name} is not supported. Defaulting to CurrentReadOrWrite")]
    partial void LogUnsupportedAccessLevel(string accessLevel, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Method SetTemperature target node {TargetNodeId} not found")]
    partial void LogMethodTargetNodeNotFound(string targetNodeId);
}
