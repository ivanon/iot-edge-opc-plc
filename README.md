# OPC UA Simulator for Fermentation Tanks (opc-simulator)

A cross-platform OPC UA server simulator built on top of the [Azure IoT Edge OPC PLC](https://github.com/Azure-Samples/iot-edge-opc-plc) codebase. It targets **fermentation tank industrial scenarios** and is being extended with an **Avalonia GUI** for visual node editing, in-process server control, and per-node dynamic data simulation.

> This project is the **server-side** component of the BioFlux platform. See the [root CLAUDE.md](../CLAUDE.md) for the overall architecture.

---

## What is this?

The original `iot-edge-opc-plc` is an excellent sample OPC UA server that generates random data, anomalies, and supports user-defined nodes via JSON configuration. However, it only provides **static** user-defined nodes — values do not change over time unless an external OPC UA client writes to them.

This project extends the original codebase to support:

- **Visual node hierarchy editor** (Avalonia GUI) — browse, add, remove, and edit nodes defined in `nodesfile.json`
- **In-process OPC UA server control** — start/stop the server directly from the GUI
- **Per-node dynamic simulation** — configure individual nodes to emit Random / Sine / Ramp / Step values automatically
- **Fermentation-tank-oriented node definitions** — default `nodesfile.json` models F11/F12/F13 tank parameters (temperature PV/SP, DO control mode, etc.)
- **Runtime editor locking** — the node editor is locked while the server is running to prevent concurrent modifications
- **Embedded resource + local persistence** — `nodesfile.json` ships as an embedded resource and is extracted to the user's local application data folder on first run

All original upstream features (anomaly simulation, boilers, alarms, deterministic alarms, chaos mode, OPC Publisher file generation, etc.) remain available via the command-line interface.

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| OPC UA Server | C# / .NET 10 / `iot-edge-opc-plc` reference stack |
| Desktop GUI | Avalonia UI 11 + ReactiveUI |
| Data Simulation | In-process timers updating variable nodes |
| Configuration | `nodesfile.json` (embedded resource + local app data) |

---

## Project Structure

```
opc-simulator/
├── src/
│   ├── opc-plc.csproj              # CLI OPC PLC project
│   ├── OpcPlc.Gui/                 # Avalonia GUI application
│   ├── PluginNodes/                # User-defined node loading & simulation logic
│   ├── OpcPlcServer.cs             # Server bootstrap
│   └── ...                         # Original upstream source files
├── tests/                          # Unit & integration tests
├── scripts/                        # Build & release scripts
│   └── build-cli-image.sh          # CLI Docker image build script
├── docs/                           # Original upstream docs (deterministic-alarms, etc.)
├── publish/                        # Self-contained publish output
├── opcplc.sln                      # Solution file
├── Dockerfile.debug                # Debug container build
├── Dockerfile.release              # Release distroless container build
└── README.md                       # This file
```

> For GUI-specific design and development documents, see [`/docs/opc-simulator/`](/docs/opc-simulator/).

---

## Build & Run (Console)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Build the console application
dotnet build src/opc-plc.csproj

# Run with default fermentation tank nodes
dotnet run --project src/opc-plc.csproj -- --nodesfile src/nodesfile.json --pn=50000 --autoaccept --unsecuretransport
```

### Build & Run (Avalonia GUI)

```bash
# Build the GUI application
dotnet build src/OpcPlc.Gui/OpcPlc.Gui.csproj

# Run the GUI
dotnet run --project src/OpcPlc.Gui/OpcPlc.Gui.csproj
```

### Publish (self-contained single-file)

```bash
# macOS ARM64 (current primary target)
dotnet publish src/OpcPlc.Gui/OpcPlc.Gui.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true \
  --output publish/osx-arm64
```

---

## `nodesfile.json` Quick Reference

The JSON file defines the OPC UA address space hierarchy. Each top-level folder represents a fermentation tank or equipment group.

```json
{
  "Folder": "Fermenter",
  "FolderList": [
    {
      "Folder": "F11",
      "NodeList": [
        {
          "NodeId": "Temperature.PV",
          "Name": "Temperature PV",
          "DataType": "Double",
          "AccessLevel": "CurrentReadOrWrite",
          "Description": "Actual temperature of tank F11"
        },
        {
          "NodeId": "Temperature.SP",
          "Name": "Temperature SP",
          "DataType": "Double",
          "AccessLevel": "CurrentReadOrWrite",
          "Description": "Temperature setpoint of tank F11"
        }
      ]
    }
  ],
  "NodeList": []
}
```

> **Important:** Every `Folder` must contain `"NodeList": []` (even if empty), otherwise the server throws a `NullReferenceException` during load.

### Field Reference

| Field | Required | Description |
|-------|----------|-------------|
| `Folder` | Yes | Name of the folder created under the server root |
| `FolderList` | No | Child folders |
| `NodeList` | Yes | List of nodes in this folder (can be empty `[]`) |
| `NodeId` | Yes | Node identifier (decimal or string) |
| `Name` | No | Display name; defaults to `NodeId` |
| `DataType` | No | OPC UA `BuiltInType`; defaults to `Int32` |
| `ValueRank` | No | Defaults to `-1` (scalar) |
| `AccessLevel` | No | Defaults to `CurrentReadOrWrite` |
| `Description` | No | Defaults to `NodeId` |

---

## Avalonia GUI

The GUI layer has been merged into `main`. See [`docs/opc-simulator/avalonia-gui.md`](/docs/opc-simulator/avalonia-gui.md) for the design spec and project structure.

GUI capabilities:
- **Visual CRUD editor** for the node tree — browse, add, remove, and edit nodes defined in `nodesfile.json`
- **In-process server start/stop** with live status indicator and real-time log panel
- **Configurable server port** — set the OPC UA listening port before starting the server
- **Per-node simulation configuration** — Random / Sine / Ramp / Step (in progress)
- **Runtime editor locking** — node editor is locked while the server is running
- **Embedded resource + local persistence** — `nodesfile.json` ships as an embedded resource and is extracted to `LocalApplicationData` on first run

---

## Docker Deployment

A **distroless CLI image** is provided for headless server deployment on Ubuntu 22.04 / linux-x64. The GUI is not included in the container.

### Build

```bash
# Using the helper script
scripts/build-cli-image.sh              # tags: bioflux/opc-simulator:latest
scripts/build-cli-image.sh v0.1.0       # tags: v0.1.0 + latest

# Or directly with Docker
docker build --platform linux/amd64 -f Dockerfile.release -t bioflux/opc-simulator:latest .
```

### Run (single instance)

```bash
docker run -d --name opcplc-sim \
    -v $PWD/nodesfile.json:/data/nodesfile.json:ro \
    -p 50000:50000 \
    bioflux/opc-simulator:latest
```

OPC UA endpoint: `opc.tcp://<host-ip>:50000`

### Run (multiple instances on same host)

```bash
docker run -d --name opcplc-f01 \
    -v $PWD/fermenter-f01.json:/data/nodesfile.json:ro \
    -p 50001:50001 bioflux/opc-simulator:latest \
    --nodesfile=/data/nodesfile.json --portnum=50001 --autoaccept

docker run -d --name opcplc-f02 \
    -v $PWD/fermenter-f02.json:/data/nodesfile.json:ro \
    -p 50002:50002 bioflux/opc-simulator:latest \
    --nodesfile=/data/nodesfile.json --portnum=50002 --autoaccept
```

> **Note:** `--portnum` inside the container must match the exposed port mapping.

### Troubleshooting

The distroless image has no shell. Use these alternatives:

```bash
docker logs -f opcplc-sim                # view logs
docker cp opcplc-sim:/app/pki ./pki-dump # extract certificates
docker run --rm bioflux/opc-simulator:latest --help  # CLI help
```

For full deployment details (certificate persistence, registry push, etc.), see [`../docs/deployment.md`](../docs/deployment.md).

---

## Design & Development Documents

All design specs, architecture decisions, and development plans for this component are stored under the root `docs/` folder:

- [`/docs/opc-simulator/avalonia-gui.md`](/docs/opc-simulator/avalonia-gui.md) — GUI design specification
- [`/docs/opc-simulator/superpowers/`](/docs/opc-simulator/superpowers/) — Brainstorming & planning artefacts

---

## Upstream Reference

This project is a derivative of the [Azure-Samples/iot-edge-opc-plc](https://github.com/Azure-Samples/iot-edge-opc-plc) repository. All original CLI options, Docker usage, certificate management, alarms, boilers, and advanced features remain documented in the upstream README and are preserved in this codebase.

Key upstream capabilities retained:
- Slow / fast / very fast changing nodes
- Spike & dip anomaly simulation
- Positive / negative trend simulation
- Boiler #1 & #2 simulations
- Simple events & alarm conditions
- Deterministic alarms (`--dalm`)
- Chaos mode (`--chaos`)
- OPC Publisher file generation (`--sph`, `--sp`)
- Full X.509 certificate store support (Directory, X509Store, FlatDirectory, KubernetesSecret)
- User certificate authentication

---

## License

This project inherits the upstream license. See [LICENSE.md](LICENSE.md) and [NOTICE.txt](NOTICE.txt) for details.
