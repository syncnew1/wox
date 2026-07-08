namespace BluetoothBattery.Core.Models;

public sealed record BluetoothDeviceSnapshot
{
    public required string StableId { get; init; }

    public required string DisplayName { get; init; }

    public string? BluetoothAddress { get; init; }

    public string? ContainerId { get; init; }

    public required string Kind { get; init; }

    public required bool IsConnected { get; init; }

    public required bool IsUserFacing { get; init; }

    public required DevicePresence Presence { get; init; }

    public required string Evidence { get; init; }

    public BatteryReading? Battery { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }

    public required IReadOnlyList<RawPnpDevice> Interfaces { get; init; }
}
