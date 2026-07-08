using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Reporting;

public sealed record DeviceSummary(
    int Total,
    int Connected,
    int WithBattery,
    int WithoutBattery,
    IReadOnlyDictionary<string, int> ByKind)
{
    public static DeviceSummary Create(IReadOnlyList<BluetoothDeviceSnapshot> devices)
    {
        var connected = devices.Count(device => device.IsConnected);
        var withBattery = devices.Count(device => device.Battery is not null);
        var byKind = devices
            .GroupBy(device => device.Kind)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count());

        return new DeviceSummary(
            devices.Count,
            connected,
            withBattery,
            devices.Count - withBattery,
            byKind);
    }
}
