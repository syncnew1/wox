# BluetoothBattery

Windows 无线设备电量识别软件。

这个项目的目标是识别当前连接的蓝牙设备和 2.4G/HID 无线设备，尽量合并同一物理设备暴露出的多个底层接口，显示更干净的设备名，并只读读取 Windows 或设备协议能提供的电量信息。

## 当前能力

- 枚举当前存在的蓝牙、HID、鼠标、键盘、音频端点、媒体和电池相关设备。
- 按蓝牙地址、设备容器、USB VID/PID 等信息合并重复接口。
- 隐藏常见噪声接口，例如 BLE GATT 服务、AVRCP 传输、USB Composite、系统蓝牙枚举器等。
- 当 Windows PnP 属性暴露电量时，读取并显示电量百分比。
- 支持读取标准 BLE GATT Battery Service，也就是 `0000180F` 服务里的 `00002A19` Battery Level 特征。
- 已加入电量 Provider 架构，后续可以继续接入 HID 电池报告、Razer/ROG/Logitech 等厂商电量查询协议。
- 支持导出诊断 JSON，方便分析设备识别和兼容性问题。
- 已创建 WinUI 3 桌面应用，支持手动刷新、自动刷新、显示电量来源和可信度。
- 项目边界：只读取电量，不修改灯效、DPI、宏、轮询率、固件或设备配置。

## 项目结构

```text
BluetoothBattery.sln
work/BluetoothBattery/
├─ config/
│  ├─ devices.sample.json
│  └─ devices.json
├─ scripts/
│  ├─ build.ps1
│  ├─ build-winui.ps1
│  ├─ install-winui-templates.ps1
│  ├─ publish-winui.ps1
│  ├─ run-cli.ps1
│  └─ run-winui.ps1
└─ src/
   ├─ BluetoothBattery.Core
   ├─ BluetoothBattery.Cli
   └─ BluetoothBattery.App
```

## 运行 CLI

在项目根目录运行：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1
```

只看摘要：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -Summary
```

导出诊断 JSON：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -JsonPath outputs\wireless-diagnostics.json
```

尝试较慢的 Windows 电量属性读取：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -Deep -TimeoutSeconds 60 -JsonPath outputs\wireless-diagnostics-deep.json
```

输出完整原始 JSON：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -Raw
```

包含被隐藏的底层接口：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -All -JsonPath outputs\wireless-all.json
```

只显示高置信度在线设备，并套用本机设备配置：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -ConnectedOnly -ConfigPath work\BluetoothBattery\config\devices.json
```

直接测试某个蓝牙 LE 设备是否暴露标准电量服务：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -BleBatteryAddress E4:81:AC:8B:3B:AC
```

查看每个设备可能适用的电量 Provider：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -ConnectedOnly -ConfigPath work\BluetoothBattery\config\devices.json -ProviderDiagnostics
```

## 设备配置

生成示例配置：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -WriteSampleConfig -ConfigPath work\BluetoothBattery\config\devices.json
```

使用配置运行：

```powershell
.\work\BluetoothBattery\scripts\run-cli.ps1 -ConfigPath work\BluetoothBattery\config\devices.json
```

配置文件支持：

- `stableId`：按稳定设备 ID 匹配。
- `nameContains`：按设备名片段匹配。
- `alias`：显示别名。
- `kind`：强制设备类型。
- `hidden`：隐藏设备。
- `notes`：备注。

## 构建

构建核心库和 CLI：

```powershell
.\work\BluetoothBattery\scripts\build.ps1
```

安装 WinUI 模板：

```powershell
.\work\BluetoothBattery\scripts\install-winui-templates.ps1
```

构建 WinUI 应用：

```powershell
.\work\BluetoothBattery\scripts\build-winui.ps1
```

运行 WinUI 应用：

```powershell
.\work\BluetoothBattery\scripts\run-winui.ps1
```

发布 WinUI 应用到 `outputs\BluetoothBattery.App`：

```powershell
.\work\BluetoothBattery\scripts\publish-winui.ps1
```

发布完成后运行：

```powershell
.\outputs\BluetoothBattery.App\BluetoothBattery.App.exe
```

## 重要说明

默认扫描模式较快，主要用于列出真实用户设备。`-Deep` 会额外尝试读取 Windows PnP 电量属性，部分机器或设备上可能较慢。

如果 CLI 或 App 提示 Windows 设备扫描被拒绝访问，请使用管理员 PowerShell 运行，或确认本机允许 PnP/CIM 设备查询。程序遇到权限错误时会明确报错，不再把权限问题误显示为“0 个设备”。

标准 BLE 电量服务已经可用。如果蓝牙设备支持 `0000180F/00002A19`，CLI 和 WinUI 会优先显示该电量。很多 2.4G 接收器和部分设备不会通过 Windows 标准属性公开电量。对于这类设备，需要后续实现厂商只读电量查询 Provider，例如 Razer、ASUS ROG、Logitech HID++ 等。

Razer Viper V2 Pro 当前已完成 OpenRazer 协议调研和 Windows HID 候选接口诊断。Windows 上正确 HID 接口会被系统或 Razer 驱动拒绝直接打开，因此不会默认触发该实验查询；后续应优先研究 `RZCONTROL/RzCommon` 的只读电量查询路径。

更多设备支持路线见 `work/BluetoothBattery/docs/provider-research.md`。
