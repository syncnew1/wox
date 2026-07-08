using System.Text.RegularExpressions;

namespace BluetoothBattery.Core.Pnp;

internal static partial class DeviceIdentity
{
    public static string? ExtractBluetoothAddress(string instanceId)
    {
        var match = DevAddressRegex().Match(instanceId);
        if (match.Success)
        {
            return NormalizeAddress(match.Groups["address"].Value);
        }

        match = TailAddressRegex().Match(instanceId);
        if (match.Success)
        {
            return NormalizeAddress(match.Groups["address"].Value);
        }

        match = EmbeddedAddressRegex().Match(instanceId);
        if (match.Success)
        {
            return NormalizeAddress(match.Groups["address"].Value);
        }

        return null;
    }

    public static string? ExtractUsbVidPid(string instanceId)
    {
        var match = UsbVidPidRegex().Match(instanceId);
        return match.Success
            ? $"VID_{match.Groups["vid"].Value.ToUpperInvariant()}&PID_{match.Groups["pid"].Value.ToUpperInvariant()}"
            : null;
    }

    public static string NormalizeAddress(string value)
    {
        var clean = value.Replace(":", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        if (clean.Length != 12)
        {
            return clean;
        }

        return string.Join(':', Enumerable.Range(0, 6).Select(i => clean.Substring(i * 2, 2)));
    }

    [GeneratedRegex(@"DEV_(?<address>[0-9A-Fa-f]{12})")]
    private static partial Regex DevAddressRegex();

    [GeneratedRegex(@"(?:&0&|\\)(?<address>[0-9A-Fa-f]{12})(?:[_\\]|$)")]
    private static partial Regex TailAddressRegex();

    [GeneratedRegex(@"[_&](?<address>[0-9A-Fa-f]{12})(?:[_&\\]|$)")]
    private static partial Regex EmbeddedAddressRegex();

    [GeneratedRegex(@"VID_(?<vid>[0-9A-Fa-f]{4})&PID_(?<pid>[0-9A-Fa-f]{4})")]
    private static partial Regex UsbVidPidRegex();
}
