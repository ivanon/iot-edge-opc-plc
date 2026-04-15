# OPCUA模拟器增强设计文档

> 目标：在 `feature/avalonia-gui` 工作树中，为 OPCUA模拟器补齐本周所需的三个核心能力：四种动态数据模拟、X509 证书 GUI 配置、`SetTemperature` 方法节点。

---

## 1. 概述

### 本周目标
1. **四种动态数据模拟**：节点支持 Random / Sine / Ramp / Step 四种模拟模式，GUI 中按模式显示对应参数表单。
2. **X509 证书 GUI 配置**：提供证书生成对话框；启动服务器时自动检测证书，有则使用安全模式，无则降级为 `--unsecuretransport` 并提示用户。
3. **`SetTemperature` 方法节点**：支持在 `nodesfile.json` 中配置 Method 节点，OPC UA Client 可通过 Python 脚本调用该方法，验证 Method Call 通路。

### 远期与中期规划（预留扩展空间）
- **中期**：Method 节点支持定制输入参数、输出结果（数量/固定值/随机值）。
- **远期**：Method 节点支持编写方法实现逻辑（脚本/表达式引擎）。

---

## 2. 动态数据模拟

### 2.1 模型变更

`SimulationConfig` 扩展现有字段：

```csharp
public class SimulationConfig
{
    public string Type { get; set; } = "Random"; // Random | Sine | Ramp | Step

    // 通用范围
    public double Min { get; set; }
    public double Max { get; set; }

    // Sine 专用
    public double PeriodMs { get; set; } = 10000;

    // Ramp 专用
    public double StepSize { get; set; } = 1.0;
    public double IntervalMs { get; set; } = 1000;

    // Step 专用
    public double StepValue { get; set; } = 1.0;
    public double HoldMs { get; set; } = 1000;
}
```

### 2.2 GUI 编辑器调整

`MainWindow.axaml` 的 `NodeItem` 编辑器模板中：
- 用 `ComboBox` 选择模式：`None`, `Random`, `Sine`, `Ramp`, `Step`
- `None` 等价于禁用模拟（`Simulation = null`）
- 根据 `Simulation.Type` 动态显示参数面板：
  - **Random**：Min, Max
  - **Sine**：Min, Max, PeriodMs
  - **Ramp**：Min, Max, StepSize, IntervalMs
  - **Step**：Min, Max, StepValue, HoldMs

### 2.3 服务端算法

`UserDefinedPluginNodes.UpdateSimulatedValue` 扩展为 `switch`：

- **Random**：`min + random * (max - min)`
- **Sine**：`min + (max - min) * 0.5 * (1 + sin(2π * t / PeriodMs))`，`t` 为服务器运行时间
- **Ramp**：维护当前值，每次 tick 增加 `StepSize`，到达 `Max` 后回绕到 `Min`
- **Step**：在两个层级间交替跳变，用 `HoldMs` 控制保持时间

所有结果统一按 `config.DataType` 转换并 clamp 到对应类型的取值范围。

---

## 3. Method 节点（`SetTemperature`）

### 3.1 JSON 模型扩展

`ConfigNode` 增加：

```csharp
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public MethodConfig Method { get; set; }
```

新增 `MethodConfig`：

```csharp
public class MethodConfig
{
    public string MethodName { get; set; } = "SetTemperature";
}
```

### 3.2 JSON 示例

```json
{
  "Folder": "F11",
  "NodeList": [
    {
      "NodeId": "TempPV",
      "Name": "Temperature",
      "DataType": "Double",
      "Value": 35.0
    },
    {
      "NodeId": "SetTemp",
      "Name": "SetTemperature",
      "Method": {
        "MethodName": "SetTemperature"
      }
    }
  ]
}
```

### 3.3 服务端实现

`UserDefinedPluginNodes.AddNodes` 中，判断 `node.Method`：

```csharp
if (node.Method != null)
{
    CreateMethod(userNodesFolder, node);
}
else
{
    var variable = CreateBaseVariable(userNodesFolder, node);
    // ... simulation setup
}
```

`CreateMethod` 实现（`SetTemperature`）：
- 输入参数：`Int32 fermenterId`, `Double targetTemp`
- 输出参数：`Boolean success`
- 方法体：在地址空间中查找 `fermenterId` 对应文件夹下的 `TempSP` 变量，写入 `targetTemp`，返回 `true`；若找不到目标变量，仍返回 `true` 并在日志中输出警告。

### 3.4 GUI 编辑器

`NodeItem` 中新增 `bool IsMethod` 和 `MethodConfig? Method`。

编辑器模板增加 **"Is Method"** CheckBox：
- 勾选后：隐藏 `DataType`、`Value`、`AccessLevel`、`Simulation` 等变量属性，仅显示 `MethodName`
- 未勾选时：`Method` 设为 `null`

互斥校验：保存时若 `IsMethod == true`，自动清空 `Simulation` 和 `Value` 等变量字段。

---

## 4. X509 证书 GUI 配置

### 4.1 功能范围

- 提供 **Certificate 设置窗口**：查看/修改证书存储目录、输入应用 URI 和组织名、设置有效期、一键生成自签名证书
- **启动服务器时自动判断**：
  - 检测到有效证书 → 传入 `--certstorepath={path}`
  - 未检测到证书 → 传入 `--unsecuretransport`，并在日志面板提示：`[Certificate] 未检测到应用证书，将以无证书模式启动 OPC UA Server。`

### 4.2 配置文件

新增 `gui-config.json`，路径按平台区分：
- Windows：`%LocalAppData%/OpcPlc.Gui/gui-config.json`
- macOS/Linux：`~/.config/OpcPlc.Gui/gui-config.json`

默认内容：

```json
{
  "CertificateStorePath": "~/.config/OpcPlc.Gui/pki",
  "ApplicationUri": "urn:OpcPlc.Gui:Server",
  "Organization": "MyOrganization",
  "CertificateLifetimeDays": 365
}
```

### 4.3 证书检查逻辑

`MainWindowViewModel.StartServerCommand` 中：

```csharp
var certPath = Path.Combine(resolvedStorePath, "own", "certs", "OpcPlc.Gui.der");
var args = new List<string> { "--nodesfile=...", ... };

if (File.Exists(certPath))
{
    args.Add($"--certstorepath={config.CertificateStorePath}");
}
else
{
    args.Add("--unsecuretransport");
    LogLines.Add("[Certificate] 未检测到应用证书，将以无证书模式启动 OPC UA Server。");
}
```

### 4.4 证书生成窗口

弹窗 `CertificateWindow` 布局：
- 证书存储路径（显示绝对路径）
- 应用 URI（文本框）
- 组织名（文本框）
- 有效期（NumericUpDown）
- 当前证书状态（存在/不存在，有效期信息）
- **"Generate Certificate"** 按钮

点击后调用 `Opc.Ua.Security.Certificates.CertificateFactory` 生成并写入 `CertificateStorePath/own/certs` 和 `own/private`。

---

## 5. 架构与数据流

```
┌─────────────────────────────────────────────┐
│           OPCUA模拟器 (Avalonia GUI)           │
├─────────────────────────────────────────────┤
│  GUI 层                                        │
│   ├── Node Editor (Folder + Variable + Method)│
│   ├── Simulation Params (Random/Sine/Ramp/Step)│
│   ├── Certificate Dialog                      │
│   ├── Start/Stop Server Buttons               │
│   └── Log Panel                               │
├─────────────────────────────────────────────┤
│  服务层                                        │
│   ├── NodesFileService (load/save JSON)       │
│   ├── CertificateService (generate X509)      │
│   └── OpcPlcServer (in-process OPC UA Server) │
├─────────────────────────────────────────────┤
│  地址空间层                                     │
│   ├── UserDefinedPluginNodes                  │
│   │   ├── CreateBaseVariable (静态变量)        │
│   │   ├── CreateMethod (SetTemperature)       │
│   │   └── UpdateSimulatedValue (4种模式)      │
│   └── Standard OPC UA NodeManager             │
└─────────────────────────────────────────────┘
```

### 关键数据流

1. **启动时**：`NodesFileService` 读取 `./nodesfile.json` → `ConfigFolder` 树 → `NodeEditorViewModel` 构建可编辑树
2. **编辑时**：用户修改 `NodeItem` → `SaveNodesCommand` 触发 → 序列化保存
3. **启动服务器时**：检查证书 → 拼接 CLI 参数 → `OpcPlcServer.StartAsync(args)`
4. **运行时**：`UserDefinedPluginNodes` 创建节点；`StartSimulation()` 为带 `Simulation` 的变量创建定时器
5. **方法调用时**：OPC UA Client 调用 `SetTemperature` → `MethodState.OnCall` → 查找并更新 `TempSP`

---

## 6. 错误处理

| 场景 | 处理方式 |
|------|----------|
| `nodesfile.json` 格式错误 | 弹窗提示，日志显示详细错误，Node Editor 保持空树 |
| 启动时端口被占用 | 捕获异常，状态回退 `Editing`，日志显示错误摘要 |
| 证书生成失败 | 弹窗显示错误，不阻塞服务器启动（可走无证书降级） |
| Method 调用找不到目标变量 | 返回 `success=true`，日志输出警告 |
| 模拟定时器异常 | 单个 tick 异常不中断其他节点，跳过本次更新 |

---

## 7. 测试计划

1. **JSON 序列化/反序列化测试**：四种 Simulation 类型 + Method 节点的 round-trip
2. **模拟算法测试**：验证 Random/Sine/Ramp/Step 数值在 `[Min, Max]` 范围内
3. **GUI 单元测试**：`NodeItem` 的 `IsMethod` 和 `Simulation` 互斥行为
4. **端到端测试**：
   - 启动 OPCUA模拟器 → Python `test_opcua.py` 连接确认变量值在变化
   - Python `call_method.py` 调用 `SetTemperature` → 确认 `TempSP` 被更新
   - 生成证书后重启 → 确认安全模式启动
   - 删除证书后重启 → 确认自动降级为 `--unsecuretransport`

---

## 8. 验收标准

- [ ] GUI 中节点的 Simulation 可切换 Random/Sine/Ramp/Step，参数表单动态变化，保存后服务端按预期模拟
- [ ] GUI 中可添加 Method 节点（`SetTemperature`），Python 脚本可成功调用并更新目标变量
- [ ] Certificate 窗口可生成自签名证书；有证书时服务器安全启动，无证书时自动降级并提示
- [ ] CLI 模式（`dotnet opc-plc.dll --pn=50000`）不受影响
- [ ] 所有变更在 `feature/avalonia-gui` 工作树中完成并提交
