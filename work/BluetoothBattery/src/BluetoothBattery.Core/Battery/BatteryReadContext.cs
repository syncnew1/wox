using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public sealed record BatteryReadContext(
    string StableId,
    string DisplayName,
    string? BluetoothAddress,
    IReadOnlyList<RawPnpDevice> Interfaces);
