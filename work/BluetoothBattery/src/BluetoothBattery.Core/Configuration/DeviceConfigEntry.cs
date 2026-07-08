namespace BluetoothBattery.Core.Configuration;

public sealed record DeviceConfigEntry
{
    public string? StableId { get; init; }

    public string? NameContains { get; init; }

    public string? Alias { get; init; }

    public string? Kind { get; init; }

    public bool? Hidden { get; init; }

    public bool? ForceShow { get; init; }

    public bool? ForceConnected { get; init; }

    public string? Notes { get; init; }
}
