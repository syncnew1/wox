using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;

namespace BluetoothBattery.Core.Battery;

public sealed class PnpPropertyBatteryProvider : IBatteryProvider
{
    public string Name => "Windows PnP properties";

    public int Priority => 100;

    public ValueTask<BatteryReading?> TryReadAsync(
        BatteryReadContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PnpBatteryReader.Read(context.Interfaces, DateTimeOffset.Now));
    }
}
