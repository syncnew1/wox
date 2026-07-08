using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Pnp;

public static class PnpBatteryReader
{
    private static readonly string[] BatteryPropertyNames =
    [
        "System.Devices.BatteryLife",
        "System.Devices.BatteryPlusCharging",
        "DEVPKEY_Device_BatteryLife",
        "DEVPKEY_Device_BatteryLevel"
    ];

    public static BatteryReading? Read(IReadOnlyList<RawPnpDevice> devices, DateTimeOffset now)
    {
        foreach (var device in devices)
        {
            foreach (var propertyName in BatteryPropertyNames)
            {
                if (!device.Properties.TryGetValue(propertyName, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                if (TryParsePercentage(raw, out var percentage))
                {
                    return new BatteryReading(
                        percentage,
                        $"Windows PnP property: {propertyName}",
                        BatteryConfidence.High,
                        now);
                }
            }
        }

        return null;
    }

    private static bool TryParsePercentage(string raw, out int percentage)
    {
        percentage = 0;
        var text = raw.Trim();

        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            text = text[..^1].Trim();
        }

        var integerPart = new string(text.TakeWhile(ch => char.IsDigit(ch) || ch == '-').ToArray());
        if (!int.TryParse(integerPart, out var value))
        {
            return false;
        }

        if (value < 0 || value > 100)
        {
            return false;
        }

        percentage = value;
        return true;
    }
}
