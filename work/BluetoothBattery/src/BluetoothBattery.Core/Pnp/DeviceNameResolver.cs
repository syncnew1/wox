using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Pnp;

internal static class DeviceNameResolver
{
    private static readonly string[] GenericNameParts =
    [
        "Bluetooth Device",
        "Microsoft Bluetooth",
        "Realtek Bluetooth",
        "RFCOMM Protocol",
        "蓝牙 LE 通用属性服务",
        "通用访问配置文件",
        "通用属性配置文件",
        "设备信息服务",
        "设备标识服务"
    ];

    public static bool IsGenericName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var normalized = Clean(name);
        return GenericNameParts.Any(part => normalized.Contains(part, StringComparison.OrdinalIgnoreCase)) ||
               normalized.Contains("HID-compliant", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("符合 HID 标准", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("USB Composite Device", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("USB Input Device", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("USB 输入设备", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("通用 USB", StringComparison.OrdinalIgnoreCase);
    }

    public static string Resolve(IReadOnlyList<RawPnpDevice> devices)
    {
        var candidates = devices
            .SelectMany(GetNameCandidates)
            .Select(Clean)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetNameScore)
            .ThenBy(name => name.Length)
            .ToArray();

        return candidates.FirstOrDefault() ?? "Unknown Bluetooth Device";
    }

    private static IEnumerable<string?> GetNameCandidates(RawPnpDevice device)
    {
        yield return GetProperty(device, "System.Devices.FriendlyName");
        yield return GetProperty(device, "System.ItemNameDisplay");
        yield return GetProperty(device, "System.Devices.ModelName");
        yield return GetProperty(device, "DEVPKEY_Device_FriendlyName");
        yield return GetProperty(device, "DEVPKEY_Device_BusReportedDeviceDesc");
        yield return GetProperty(device, "DEVPKEY_NAME");
        yield return device.FriendlyName;
    }

    private static string? GetProperty(RawPnpDevice device, string key)
    {
        return device.Properties.TryGetValue(key, out var value) ? value : null;
    }

    public static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var result = name.Trim();
        result = result.Replace(" Avrcp 传输", string.Empty, StringComparison.OrdinalIgnoreCase);
        result = result.Replace(" AVRCP Transport", string.Empty, StringComparison.OrdinalIgnoreCase);
        return result.Trim();
    }

    private static int GetNameScore(string name)
    {
        if (GenericNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase)))
        {
            return 100;
        }

        if (name.Contains("Avrcp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("传输", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (name.StartsWith("LE_", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 0;
    }
}
