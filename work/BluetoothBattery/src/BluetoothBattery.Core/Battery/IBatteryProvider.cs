using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public interface IBatteryProvider
{
    string Name { get; }

    int Priority { get; }

    ValueTask<BatteryReading?> TryReadAsync(BatteryReadContext context, CancellationToken cancellationToken);
}
