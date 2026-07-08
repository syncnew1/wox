using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBattery.Core.Hid;

public sealed partial class WindowsHidDeviceEnumerator
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int InvalidHandleValue = -1;
    private const int HidpStatusSuccess = 0x00110000;

    public IReadOnlyList<HidDeviceInfo> Enumerate(ushort? vendorId = null, ushort? productId = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == new IntPtr(InvalidHandleValue))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var result = new List<HidDeviceInfo>();
            uint index = 0;
            while (true)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    CbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
                };

                if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                var path = GetDevicePath(deviceInfoSet, interfaceData);
                if (!string.IsNullOrWhiteSpace(path) &&
                    TryReadAttributes(path, out var item) &&
                    (!vendorId.HasValue || item.VendorId == vendorId.Value) &&
                    (!productId.HasValue || item.ProductId == productId.Value))
                {
                    result.Add(item);
                }

                index++;
            }

            return result
                .OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? GetDevicePath(IntPtr deviceInfoSet, SpDeviceInterfaceData interfaceData)
    {
        SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
        if (requiredSize == 0)
        {
            return null;
        }

        var detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            var cbSize = IntPtr.Size == 8 ? 8 : 5;
            Marshal.WriteInt32(detailDataBuffer, cbSize);
            if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    detailDataBuffer,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
            {
                return null;
            }

            return Marshal.PtrToStringAuto(detailDataBuffer + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(detailDataBuffer);
        }
    }

    private static bool TryReadAttributes(string devicePath, out HidDeviceInfo info)
    {
        info = default!;
        using var handle = CreateFile(
            devicePath,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return false;
        }

        var attributes = new HiddAttributes
        {
            Size = Marshal.SizeOf<HiddAttributes>()
        };
        if (!HidD_GetAttributes(handle, ref attributes))
        {
            return false;
        }

        ushort usagePage = 0;
        ushort usage = 0;
        ushort featureReportLength = 0;

        if (HidD_GetPreparsedData(handle, out var preparsedData))
        {
            try
            {
                if (HidP_GetCaps(preparsedData, out var caps) == HidpStatusSuccess)
                {
                    usagePage = caps.UsagePage;
                    usage = caps.Usage;
                    featureReportLength = caps.FeatureReportByteLength;
                }
            }
            finally
            {
                HidD_FreePreparsedData(preparsedData);
            }
        }

        info = new HidDeviceInfo(
            devicePath,
            ExtractInstanceId(devicePath),
            attributes.VendorId,
            attributes.ProductId,
            usagePage,
            usage,
            featureReportLength);
        return true;
    }

    private static string ExtractInstanceId(string devicePath)
    {
        var match = HidPathRegex().Match(devicePath);
        if (!match.Success)
        {
            return devicePath;
        }

        return match.Groups["id"].Value
            .Replace('#', '\\')
            .ToUpperInvariant();
    }

    [GeneratedRegex(@"\\\\\?\\hid#(?<id>.+?)#\{", RegexOptions.IgnoreCase)]
    private static partial Regex HidPathRegex();

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        [MarshalAs(UnmanagedType.LPTStr)] string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int CbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }
}
