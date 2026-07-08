using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Configuration;

public sealed class DeviceConfigApplier(DeviceConfig config)
{
    public IReadOnlyList<BluetoothDeviceSnapshot> Apply(IReadOnlyList<BluetoothDeviceSnapshot> devices)
    {
        return devices
            .Select(Apply)
            .Where(device => device.IsUserFacing)
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public BluetoothDeviceSnapshot Apply(BluetoothDeviceSnapshot device)
    {
        var entry = FindEntry(device);
        if (entry is null)
        {
            return device;
        }

        return device with
        {
            DisplayName = string.IsNullOrWhiteSpace(entry.Alias) ? device.DisplayName : entry.Alias,
            Kind = string.IsNullOrWhiteSpace(entry.Kind) ? device.Kind : entry.Kind,
            IsUserFacing = ResolveVisibility(device, entry),
            IsConnected = entry.ForceConnected ?? device.IsConnected,
            Presence = ResolvePresence(device, entry),
            Evidence = string.IsNullOrWhiteSpace(entry.Notes) ? device.Evidence : $"{device.Evidence}; config: {entry.Notes}"
        };
    }

    private static bool ResolveVisibility(BluetoothDeviceSnapshot device, DeviceConfigEntry entry)
    {
        if (entry.Hidden == true)
        {
            return false;
        }

        if (entry.ForceShow == true)
        {
            return true;
        }

        return device.IsUserFacing;
    }

    private static DevicePresence ResolvePresence(BluetoothDeviceSnapshot device, DeviceConfigEntry entry)
    {
        if (entry.Hidden == true)
        {
            return DevicePresence.Noise;
        }

        if (entry.ForceConnected == true)
        {
            return DevicePresence.ConnectedConfirmed;
        }

        if (entry.ForceShow == true && device.Presence < DevicePresence.LikelyActive)
        {
            return DevicePresence.LikelyActive;
        }

        return device.Presence;
    }

    private DeviceConfigEntry? FindEntry(BluetoothDeviceSnapshot device)
    {
        return config.Devices.FirstOrDefault(entry =>
            MatchesStableId(entry, device) || MatchesName(entry, device));
    }

    private static bool MatchesStableId(DeviceConfigEntry entry, BluetoothDeviceSnapshot device)
    {
        return !string.IsNullOrWhiteSpace(entry.StableId) &&
               string.Equals(entry.StableId, device.StableId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesName(DeviceConfigEntry entry, BluetoothDeviceSnapshot device)
    {
        return !string.IsNullOrWhiteSpace(entry.NameContains) &&
               device.DisplayName.Contains(entry.NameContains, StringComparison.OrdinalIgnoreCase);
    }
}
