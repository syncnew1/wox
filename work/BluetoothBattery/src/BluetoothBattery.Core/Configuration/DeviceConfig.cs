namespace BluetoothBattery.Core.Configuration;

public sealed record DeviceConfig
{
    public IReadOnlyList<DeviceConfigEntry> Devices { get; init; } = [];
}
