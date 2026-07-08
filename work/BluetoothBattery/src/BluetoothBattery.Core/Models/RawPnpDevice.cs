namespace BluetoothBattery.Core.Models;

public sealed record RawPnpDevice
{
    public required string InstanceId { get; init; }

    public string? Class { get; init; }

    public string? FriendlyName { get; init; }

    public string? Status { get; init; }

    public required IReadOnlyDictionary<string, string?> Properties { get; init; }
}
