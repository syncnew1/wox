using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace BluetoothBattery.Core.Hid;

public sealed class WindowsHidFeatureReport
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    public byte[] GetFeature(string devicePath, byte reportId, int length)
    {
        var buffer = new byte[length];
        buffer[0] = reportId;

        using var handle = Open(devicePath);
        if (!HidD_GetFeature(handle, buffer, buffer.Length))
        {
            throw new InvalidOperationException($"HidD_GetFeature failed: {Marshal.GetLastWin32Error()}");
        }

        return buffer;
    }

    public void SetFeature(string devicePath, byte[] report)
    {
        using var handle = Open(devicePath);
        if (!HidD_SetFeature(handle, report, report.Length))
        {
            throw new InvalidOperationException($"HidD_SetFeature failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private static SafeFileHandle Open(string devicePath)
    {
        var handle = CreateFile(
            devicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new InvalidOperationException($"Failed to open HID device: {Marshal.GetLastWin32Error()}");
        }

        return handle;
    }

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
