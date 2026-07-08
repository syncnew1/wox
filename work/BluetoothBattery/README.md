# BluetoothBattery

Windows 无线设备电量监控 MVP。

这个项目的目标是识别当前连接的蓝牙设备和 2.4G/HID 无线设备，尽量合并同一物理设备暴露出的多个底层接口，显示更干净的设备名，并读取 Windows 或设备协议能提供的电量信息。

## 当前能力

- 枚举当前存在的蓝牙、HID、鼠标、键盘、音频端点、媒体和电池相关设备。
- 按蓝牙地址、设备容器、USB VID/PID 等信息合并重复接口。
- 隐藏常见噪声接口，例如 BLE GATT 服务、AVRCP 传输、USB Composite、系统蓝牙枚举器等。
- 当 Windows PnP 属性暴露电量时，读取并显示电量百分比。
- 支持读取标准 BLE GATT Battery Service，也就是 `0000180F` 服务里的 `00002A19` Battery Level 特征。
- 已加入电量 Provider 架构，后续可以继续接入 HID 电池报告、Razer/ROG/Logitech 等厂商协议。
- 支持导出诊断 JSON，方便分析设备识别和兼容性问题。
- 已创建 WinUI 3 桌面应用骨架，并接入现有核心扫描逻辑。

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

## 重要说明

默认扫描模式较快，主要用于列出真实用户设备。`-Deep` 会额外尝试读取 Windows PnP 电量属性，部分机器或设备上可能较慢。

标准 BLE 电量服务已经可用。如果蓝牙设备支持 `0000180F/00002A19`，CLI 和 WinUI 会优先显示该电量。很多 2.4G 接收器和部分设备不会通过 Windows 标准属性公开电量。对于这类设备，需要后续实现厂商协议 Provider，例如 Razer、ASUS ROG、Logitech HID++ 等。
