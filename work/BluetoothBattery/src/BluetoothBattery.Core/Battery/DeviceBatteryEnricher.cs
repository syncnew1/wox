using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public sealed class DeviceBatteryEnricher
{
    private readonly CompositeBatteryProvider _provider = new(
    [
        new BleGattBatteryProvider(),
        new PnpPropertyBatteryProvider(),
        new PlannedProvider("Vendor-specific HID provider", 300)
    ]);

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> EnrichAsync(
        IReadOnlyList<BluetoothDeviceSnapshot> devices,
        CancellationToken cancellationToken)
    {
        var result = new List<BluetoothDeviceSnapshot>(devices.Count);

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (device.Battery is not null ||
                !device.IsUserFacing ||
                device.Presence < DevicePresence.LikelyActive)
            {
                result.Add(device);
                continue;
            }

            using var perDeviceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perDeviceTimeout.CancelAfter(TimeSpan.FromSeconds(5));

            var reading = await TryReadAsync(device, perDeviceTimeout.Token).ConfigureAwait(false);
            result.Add(reading is null ? device : device with { Battery = reading });
        }

        return result;
    }

    private async ValueTask<BatteryReading?> TryReadAsync(
        BluetoothDeviceSnapshot device,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = new BatteryReadContext(
                device.StableId,
                device.DisplayName,
                device.BluetoothAddress,
                device.Interfaces);

            return await _provider.TryReadAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
