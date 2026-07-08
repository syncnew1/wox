using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Battery;

public sealed class CompositeBatteryProvider(IEnumerable<IBatteryProvider> providers)
{
    private readonly IReadOnlyList<IBatteryProvider> _providers = providers
        .OrderBy(provider => provider.Priority)
        .ToArray();

    public async ValueTask<BatteryReading?> TryReadAsync(
        BatteryReadContext context,
        CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            var reading = await provider.TryReadAsync(context, cancellationToken).ConfigureAwait(false);
            if (reading is not null)
            {
                return reading;
            }
        }

        return null;
    }
}
