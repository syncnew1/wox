# 设备电量 Provider 调研

目标：让项目支持尽可能多的蓝牙、2.4G 和 HID 无线设备。策略不是把所有设备写死在扫描器里，而是把电量读取拆成多个 Provider，按置信度依次尝试。

范围边界：本项目只读取电量。即使参考 OpenRazer、Solaar、ckb-next 等项目，也只借鉴电量查询相关代码，不实现灯效、DPI、宏、轮询率、低电量阈值、固件、配置写入等功能。

## 已验证

### 标准 BLE Battery Service

- 适用设备：暴露 GATT Battery Service 的蓝牙键盘、鼠标、耳机、手柄等。
- 协议：
  - Service UUID: `0000180F-0000-1000-8000-00805F9B34FB`
  - Characteristic UUID: `00002A19-0000-1000-8000-00805F9B34FB`
- 当前状态：已实现 `BleGattBatteryProvider`。
- 本机验证：
  - `ROG FALCHION RX LOW PROFILE`
  - 地址：`E4:81:AC:8B:3B:AC`
  - 返回：`57%`

## 高价值开源项目

### OpenRazer

- 仓库：`https://github.com/openrazer/openrazer`
- 价值：覆盖大量 Razer 鼠标、键盘、耳机、接收器，包含 Linux HID 驱动和设备 PID 列表。
- 与本机设备相关：
  - `Razer Viper V2 Pro (Wireless)` 是 `1532:00A6`
  - OpenRazer 已把该设备列为支持电量读取的鼠标。
- 关键源码点：
  - `driver/razerchromacommon.c`
    - `razer_chroma_misc_get_battery_level()`
    - 请求：`get_razer_report(0x07, 0x80, 0x02)`
    - 返回：电量原始值在 `arguments[1]`
  - `driver/razermouse_driver.c`
    - `razer_attr_read_charge_level`
    - 注释说明返回值需要从 `0..255` 换算到 `0..100`
    - `Viper V2 Pro Wireless/Wired` 使用 `transaction_id.id = 0x1f`
- Windows 接入难点：
  - Linux 版本通过内核 HID 驱动发送 `razer_report`。
  - Windows 需要找到可打开的 HID interface，并用 HidD_SetFeature/HidD_GetFeature 或 WinUSB/Razer Control Device 发送等价电量查询 payload。
  - 不能把未知响应当成正式电量，必须先走诊断模式确认 report layout。
- 本机 Windows 观察：
  - `VID_1532&PID_00A6&MI_00` 存在 feature report 长度 `91` 的 HID interface。
  - 直接打开该 HID interface 返回 `Access denied`，说明它可能被 Windows HID mouse stack 或 Razer 驱动占用/保护。
  - 系统存在 `RZCONTROL\VID_1532&PID_00A6&MI_00`，服务为 `RzCommon`。后续如果要读取 Razer 电量，应优先研究 Razer Control Device 的只读查询接口，而不是强行占用鼠标 HID。

### Solaar / Logitech HID++

- 仓库：`https://github.com/pwr-Solaar/Solaar`
- 价值：Logitech Unifying/Bolt/HID++ 设备覆盖非常广，尤其适合 2.4G 键鼠。
- 可借鉴：
  - HID++ 1.0/2.0 feature 探测。
  - Battery Status/Battery Voltage/Unified Battery 相关 feature。
- 建议 Provider：
  - `LogitechHidppBatteryProvider`
  - 按 VID/PID、Unifying/Bolt receiver、HID++ feature set 探测。

### libratbag / Piper

- 仓库：`https://github.com/libratbag/libratbag`
- 价值：游戏鼠标数据库和 HID 协议抽象，覆盖 Logitech、Roccat、SteelSeries、Etekcity 等部分设备。
- 可借鉴：
  - 设备数据文件、驱动分发方式、按设备 capability 决定功能。
- 建议用途：
  - 作为设备能力数据库参考，不直接照搬 UI 逻辑。

### OpenRGB

- 仓库：`https://github.com/CalcProgrammer1/OpenRGB`
- 价值：大量外设 HID/USB 控制代码，部分设备族协议可参考。
- 限制：
  - 核心关注 RGB，不是电量；只作为设备识别和 HID 打开方式参考，不实现 RGB 控制。

### ckb-next

- 仓库：`https://github.com/ckb-next/ckb-next`
- 价值：Corsair 键盘、鼠标、耳机协议，含部分无线设备状态。
- 建议 Provider：
  - `CorsairBatteryProvider`
  - 优先用于 Corsair Slipstream/USB dongle 设备。

### asusctl / hid-asus-rog

- 相关项目：
  - `https://gitlab.com/asus-linux/asusctl`
  - Linux kernel `hid-asus`
- 价值：ROG 外设和 ASUS 设备 HID 行为参考。
- 当前本机 ROG 键盘已通过 BLE 标准电量解决，后续可用来补充 2.4G ROG Omni Receiver 模式。

## Provider 优先级建议

1. `BleGattBatteryProvider`
   - 标准协议，置信度高。
2. `WindowsPnpBatteryProvider`
   - 读取 Windows 公开属性，低风险。
3. `LogitechHidppBatteryProvider`
   - 覆盖面大，优先级高。
4. `RazerHidBatteryProvider`
   - 先支持 OpenRazer 明确支持且本机可验证的 PID。
5. `CorsairBatteryProvider`
   - 覆盖 ckb-next 支持设备。
6. `AsusRogHidBatteryProvider`
   - ROG Omni/Armoury Crate 相关设备。
7. `VendorDiagnosticProvider`
   - 不输出正式电量，只导出原始响应供适配新设备。

## 下一步实现顺序

1. 加 `--provider-diagnostics` CLI 命令，输出每个设备可用 Provider、候选接口、VID/PID、MI、COL、Usage 信息。
2. 加 Windows HID 打开层：
   - 枚举 HID device path。
   - 支持 Feature Report 读写。
   - 只允许电量查询所需的 feature report，不做灯效/DPI/配置写入。
3. 针对 `1532:00A6` 实现 Razer battery 探测：
   - 请求等价 OpenRazer `0x07/0x80/0x02`。
   - `transaction_id = 0x1f`。
   - 响应 `arguments[1]` 按 `round(value * 100 / 255)` 换算。
   - 仅当响应状态、checksum、值域都可信时返回。
4. 如果 Windows HID interface 被拒绝访问，改走 `RZCONTROL/RzCommon` 的只读查询路径。
5. 再接 Logitech HID++，这是覆盖 2.4G 设备最多的一条线。
