namespace BluetoothBattery.Core.Models;

public enum DevicePresence
{
    Noise = 0,
    PairedOnly = 1,
    ReceiverOnly = 2,
    LikelyActive = 3,
    ConnectedConfirmed = 4
}
