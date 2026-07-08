using BluetoothBattery.Core.Hid;
using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public sealed class RazerBatteryProbe
{
    private const ushort RazerVendorId = 0x1532;
    private const ushort ViperV2ProWirelessProductId = 0x00A6;
    private const int WindowsReportLength = 91;
    private const int RazerReportOffset = 1;
    private const byte ReportId = 0x00;

    private readonly WindowsHidDeviceEnumerator _enumerator = new();
    private readonly WindowsHidFeatureReport _featureReport = new();

    public BatteryReading? TryReadViperV2Pro(CancellationToken cancellationToken)
    {
        return TryReadViperV2ProWithDiagnostics(cancellationToken).Reading;
    }

    public RazerBatteryProbeResult TryReadViperV2ProWithDiagnostics(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = _enumerator
            .Enumerate(RazerVendorId, ViperV2ProWirelessProductId)
            .Where(device => device.FeatureReportByteLength == WindowsReportLength)
            .OrderBy(device => device.InstanceId.Contains("MI_00", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToArray();

        var attempts = new List<string>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var request = CreateBatteryRequest();
                _featureReport.SetFeature(candidate.DevicePath, request);
                Thread.Sleep(250);

                var response = _featureReport.GetFeature(candidate.DevicePath, ReportId, WindowsReportLength);
                var percentage = TryParseBatteryPercentage(response);
                if (percentage is not null)
                {
                    return new RazerBatteryProbeResult(new BatteryReading(
                        percentage.Value,
                        "Razer HID battery query",
                        BatteryConfidence.Medium,
                        DateTimeOffset.Now), attempts);
                }

                attempts.Add($"{candidate.InstanceId}: unexpected response {FormatResponseHeader(response)}");
            }
            catch (Exception ex)
            {
                attempts.Add($"{candidate.InstanceId}: {ex.Message}");
            }
        }

        return new RazerBatteryProbeResult(null, attempts);
    }

    private static byte[] CreateBatteryRequest()
    {
        var report = new byte[WindowsReportLength];
        report[0] = ReportId;

        // Razer report layout begins at byte 1 because Windows feature reports reserve byte 0 for report id.
        report[RazerReportOffset + 0] = 0x00; // status: new command
        report[RazerReportOffset + 1] = 0x1f; // transaction id for Viper V2 Pro family
        report[RazerReportOffset + 2] = 0x00; // remaining packets high byte
        report[RazerReportOffset + 3] = 0x00; // remaining packets low byte
        report[RazerReportOffset + 4] = 0x00; // protocol type
        report[RazerReportOffset + 5] = 0x02; // data size
        report[RazerReportOffset + 6] = 0x07; // command class: misc
        report[RazerReportOffset + 7] = 0x80; // command id: get battery level
        report[RazerReportOffset + 88] = CalculateRazerCrc(report);

        return report;
    }

    private static int? TryParseBatteryPercentage(byte[] response)
    {
        if (response.Length < WindowsReportLength)
        {
            return null;
        }

        var status = response[RazerReportOffset + 0];
        var transactionId = response[RazerReportOffset + 1];
        var dataSize = response[RazerReportOffset + 5];
        var commandClass = response[RazerReportOffset + 6];
        var commandId = response[RazerReportOffset + 7];
        var rawBattery = response[RazerReportOffset + 9];

        if (status != 0x02 ||
            transactionId != 0x1f ||
            dataSize < 0x02 ||
            commandClass != 0x07 ||
            commandId != 0x80)
        {
            return null;
        }

        if (rawBattery is 0x00 or 0xff)
        {
            return null;
        }

        return (int)Math.Round(rawBattery * 100.0 / 255.0);
    }

    private static string FormatResponseHeader(byte[] response)
    {
        if (response.Length < 12)
        {
            return Convert.ToHexString(response);
        }

        return Convert.ToHexString(response.Take(16).ToArray());
    }

    private static byte CalculateRazerCrc(byte[] report)
    {
        byte crc = 0;
        for (var i = RazerReportOffset + 2; i < RazerReportOffset + 88; i++)
        {
            crc ^= report[i];
        }

        return crc;
    }
}

public sealed record RazerBatteryProbeResult(
    BatteryReading? Reading,
    IReadOnlyList<string> Attempts);
