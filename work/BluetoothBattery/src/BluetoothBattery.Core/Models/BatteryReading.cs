namespace BluetoothBattery.Core.Models;

public sealed record BatteryReading(
    int Percentage,
    string Source,
    BatteryConfidence Confidence,
    DateTimeOffset ReadAt);
