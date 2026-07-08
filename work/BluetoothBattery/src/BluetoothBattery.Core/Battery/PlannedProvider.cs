using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public sealed class PlannedProvider(string name, int priority) : IBatteryProvider
{
    public string Name { get; } = name;

    public int Priority { get; } = priority;

    public ValueTask<BatteryReading?> TryReadAsync(
        BatteryReadContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<BatteryReading?>(null);
    }
}
