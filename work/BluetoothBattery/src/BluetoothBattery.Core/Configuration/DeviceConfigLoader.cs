using System.Text.Json;

namespace BluetoothBattery.Core.Configuration;

public static class DeviceConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static DeviceConfig LoadOrDefault(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new DeviceConfig();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DeviceConfig>(json, JsonOptions) ?? new DeviceConfig();
    }

    public static void WriteSample(string path)
    {
        var config = new DeviceConfig
        {
            Devices =
            [
                new DeviceConfigEntry
                {
                    StableId = "usb:VID_1532&PID_00A6",
                    Alias = "Razer Viper V2 Pro",
                    Kind = "Mouse",
                    ForceShow = true,
                    ForceConnected = true,
                    Notes = "2.4G receiver / wired USB composite device"
                },
                new DeviceConfigEntry
                {
                    NameContains = "Zako Virtual Mouse",
                    Hidden = true,
                    Notes = "Example hidden virtual device"
                }
            ]
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }
}
