using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;
using BluetoothBattery.Core.Hid;

namespace BluetoothBattery.Core.Battery;

public sealed class BatteryProviderDiagnostics
{
    private readonly WindowsHidDeviceEnumerator _hidDeviceEnumerator = new();

    private static readonly HashSet<string> KnownRazerBatteryVidPids = new(StringComparer.OrdinalIgnoreCase)
    {
        "VID_1532&PID_00A6"
    };

    public IReadOnlyList<BatteryProviderDiagnostic> Analyze(IReadOnlyList<BluetoothDeviceSnapshot> devices)
    {
        return devices.SelectMany(AnalyzeDevice).ToArray();
    }

    private IEnumerable<BatteryProviderDiagnostic> AnalyzeDevice(BluetoothDeviceSnapshot device)
    {
        yield return new BatteryProviderDiagnostic(
            device.StableId,
            device.DisplayName,
            device.Kind,
            "BLE GATT Battery Service",
            string.IsNullOrWhiteSpace(device.BluetoothAddress) ? "NotApplicable" : "Candidate",
            string.IsNullOrWhiteSpace(device.BluetoothAddress)
                ? "No Bluetooth LE address found."
                : $"Bluetooth address: {device.BluetoothAddress}");

        yield return new BatteryProviderDiagnostic(
            device.StableId,
            device.DisplayName,
            device.Kind,
            "Windows PnP properties",
            "Candidate",
            "Can read DEVPKEY/System.Devices battery properties when Windows exposes them.");

        var usbVidPid = FindUsbVidPid(device);
        if (usbVidPid is null)
        {
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "Vendor HID",
                "NotApplicable",
                "No USB VID/PID found.");
            yield break;
        }

        if (usbVidPid.StartsWith("VID_1532&", StringComparison.OrdinalIgnoreCase))
        {
            var hidDevices = TryEnumerateHid(0x1532, ParseProductId(usbVidPid));
            var status = KnownRazerBatteryVidPids.Contains(usbVidPid) ? "Planned" : "Candidate";
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "Razer HID/OpenRazer",
                status,
                status == "Planned"
                    ? $"{usbVidPid}; read-only battery query maps to OpenRazer command 0x07/0x80/0x02 with transaction id 0x1f. HID candidates: {FormatHidCandidates(hidDevices)}"
                    : $"{usbVidPid}; Razer device detected, protocol needs per-device verification.");
        }
        else if (usbVidPid.StartsWith("VID_046D&", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "Logitech HID++/Solaar",
                "Candidate",
                $"{usbVidPid}; use HID++ battery feature probing.");
        }
        else if (usbVidPid.StartsWith("VID_1B1C&", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "Corsair ckb-next",
                "Candidate",
                $"{usbVidPid}; Corsair protocol support should be checked against ckb-next.");
        }
        else if (usbVidPid.StartsWith("VID_0B05&", StringComparison.OrdinalIgnoreCase) ||
                 device.DisplayName.Contains("ROG", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "ASUS ROG HID",
                "Candidate",
                $"{usbVidPid}; ROG/ASUS protocol support should be checked against asusctl/hid-asus.");
        }
        else
        {
            yield return new BatteryProviderDiagnostic(
                device.StableId,
                device.DisplayName,
                device.Kind,
                "Vendor HID",
                "Unknown",
                $"{usbVidPid}; no mapped provider yet.");
        }
    }

    private static string? FindUsbVidPid(BluetoothDeviceSnapshot device)
    {
        if (device.StableId.StartsWith("usb:", StringComparison.OrdinalIgnoreCase))
        {
            return device.StableId["usb:".Length..];
        }

        return device.Interfaces
            .Select(item => DeviceIdentity.ExtractUsbVidPid(item.InstanceId))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private IReadOnlyList<HidDeviceInfo> TryEnumerateHid(ushort vendorId, ushort? productId)
    {
        try
        {
            return _hidDeviceEnumerator.Enumerate(vendorId, productId);
        }
        catch
        {
            return [];
        }
    }

    private static ushort? ParseProductId(string usbVidPid)
    {
        const string token = "PID_";
        var index = usbVidPid.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || usbVidPid.Length < index + token.Length + 4)
        {
            return null;
        }

        return ushort.TryParse(
            usbVidPid.Substring(index + token.Length, 4),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static string FormatHidCandidates(IReadOnlyList<HidDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            return "none opened";
        }

        return string.Join("; ", devices.Select(device =>
            $"usage=0x{device.UsagePage:X4}/0x{device.Usage:X4}, featureLen={device.FeatureReportByteLength}, id={device.InstanceId}"));
    }
}
