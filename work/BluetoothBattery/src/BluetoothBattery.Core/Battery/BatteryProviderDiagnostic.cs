namespace BluetoothBattery.Core.Battery;

public sealed record BatteryProviderDiagnostic(
    string StableId,
    string DisplayName,
    string Kind,
    string Provider,
    string Status,
    string Details);
