# Avalonia GUI for OpcPlc

A cross-platform desktop GUI for `iot-edge-opc-plc` built with Avalonia UI 11 and ReactiveUI.

## Features

- **Visual Node Editor**: Browse and edit the OPC UA node hierarchy defined in `nodesfile.json`
  - Add/remove folders and nodes
  - Edit node properties (NodeId, Name, DataType, ValueRank, AccessLevel, Description, Value)
  - Editor is locked while the OPC UA server is running to prevent concurrent modifications
- **Embedded Resource**: `nodesfile.json` is embedded into the application and extracted to the user's local application data folder at first run. Changes are persisted across restarts.
- **In-Process Server Control**: Start and stop the OPC UA server directly from the GUI with live status
- **Live Logs**: Scrolling log panel showing server output in real time
- **Per-Node Random Simulation**: Configure individual nodes to emit random values within a Min/Max range. Simulation runs automatically when the server starts.

## Build

Requires .NET 10 SDK.

```bash
dotnet build src/OpcPlc.Gui/OpcPlc.Gui.csproj
```

## Run

```bash
dotnet run --project src/OpcPlc.Gui/OpcPlc.Gui.csproj
```

## Test

```bash
dotnet test src/OpcPlc.Gui.Tests/OpcPlc.Gui.Tests.csproj
```

## Publish (Single File)

```bash
dotnet publish src/OpcPlc.Gui/OpcPlc.Gui.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish
```

> Note: `.uanodes` resource loading was fixed to resolve relative to `AppContext.BaseDirectory` so single-file publish works correctly.

## Project Structure

| Path | Description |
|------|-------------|
| `src/OpcPlc.Gui/` | Avalonia application (Views, ViewModels, Services) |
| `src/OpcPlc.Gui.Tests/` | xUnit test project |
| `src/PluginNodes/SimulationConfig.cs` | Simulation configuration model |
| `src/PluginNodes/UserDefinedPluginNodes.cs` | Loads `nodesfile.json` and runs per-node simulation timers |
| `src/nodesfile.json` | Default node hierarchy (embedded resource) |

## Usage

1. Launch the GUI
2. Edit the node tree on the left; select a node to configure properties and simulation on the right
3. Click **Save** to persist changes
4. Click **Start Server** to launch the in-process OPC UA server
5. Connect with any OPC UA client (e.g., `opc.tcp://127.0.0.1:50000`)
6. Click **Stop Server** to shut down
