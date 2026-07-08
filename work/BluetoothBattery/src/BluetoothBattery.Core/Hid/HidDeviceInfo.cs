namespace BluetoothBattery.Core.Hid;

public sealed record HidDeviceInfo(
    string DevicePath,
    string InstanceId,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    ushort FeatureReportByteLength);
