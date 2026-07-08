using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BluetoothBattery.Core.Battery;
using BluetoothBattery.Core.Models;

namespace BluetoothBattery.Core.Pnp;

public sealed class PnpDeviceScanner
{
    private static readonly CompositeBatteryProvider BatteryProvider = new(
    [
        new PnpPropertyBatteryProvider(),
        new PlannedProvider("BLE GATT Battery Service", 200),
        new PlannedProvider("Vendor-specific HID provider", 300)
    ]);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> ScanAsync(
        bool deepScan = false,
        bool connectedOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This scanner uses Windows PnP device APIs through PowerShell.");
        }

        var rawDevices = await ReadRawDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (deepScan)
        {
            rawDevices = await EnrichBatteryPropertiesAsync(rawDevices, cancellationToken).ConfigureAwait(false);
        }
        var now = DateTimeOffset.Now;

        return rawDevices
            .GroupBy(GetStableGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildSnapshot(group.Key, group.ToArray(), now))
            .Where(device => device.IsUserFacing)
            .Where(device => !connectedOnly || device.Presence >= DevicePresence.LikelyActive)
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> ScanAllAsync(
        bool deepScan = false,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This scanner uses Windows PnP device APIs through PowerShell.");
        }

        var rawDevices = await ReadRawDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (deepScan)
        {
            rawDevices = await EnrichBatteryPropertiesAsync(rawDevices, cancellationToken).ConfigureAwait(false);
        }

        var now = DateTimeOffset.Now;
        return rawDevices
            .GroupBy(GetStableGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildSnapshot(group.Key, group.ToArray(), now))
            .OrderByDescending(device => device.IsUserFacing)
            .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string ToJson(IReadOnlyList<BluetoothDeviceSnapshot> devices)
    {
        return JsonSerializer.Serialize(devices, JsonOptions);
    }

    private static async Task<IReadOnlyList<RawPnpDevice>> ReadRawDevicesAsync(CancellationToken cancellationToken)
    {
        var stdout = await RunPowerShellAsync(FastScanScript, null, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(stdout) ? [] : DeserializeRawDevices(stdout);
    }

    private const string FastScanScript = """
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $ErrorActionPreference = 'SilentlyContinue'

            $classes = @('Bluetooth', 'HIDClass', 'Mouse', 'Keyboard', 'AudioEndpoint', 'Media', 'Battery', 'USB')
            $deviceMap = [ordered]@{}
            foreach ($class in $classes) {
              Get-PnpDevice -Class $class -PresentOnly -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_.InstanceId) {
                  $deviceMap[$_.InstanceId] = [pscustomobject]@{
                    InstanceId = $_.InstanceId
                    Class = $_.Class
                    FriendlyName = $_.FriendlyName
                    Status = $_.Status
                    Manufacturer = $null
                    Description = $_.FriendlyName
                  }
                }
              }
            }

            if ($deviceMap.Count -eq 0) {
              foreach ($class in $classes) {
                Get-CimInstance -ClassName Win32_PnPEntity -Filter "PNPClass = '$class'" -ErrorAction SilentlyContinue | ForEach-Object {
                  if ($_.PNPDeviceID) {
                    $deviceMap[$_.PNPDeviceID] = [pscustomobject]@{
                      InstanceId = $_.PNPDeviceID
                      Class = $_.PNPClass
                      FriendlyName = $_.Name
                      Status = 'OK'
                      Manufacturer = $_.Manufacturer
                      Description = $_.Description
                    }
                  }
                }
              }
            }

            $devices = $deviceMap.Values | Where-Object {
              ($_.Status -eq 'OK') -and (
                ($_.InstanceId -match '^(BTH|BTHLE|BTHENUM|HID\\|USB\\VID_)') -or
                ($_.FriendlyName -match '(Bluetooth|Wireless|2\.4G|2.4 GHz|Receiver|Unifying|Bolt|Logitech|Razer|Mouse|Keyboard|Controller|Headset|Headphone|Buds|WH-)') -or
                ($_.Description -match '(Bluetooth|Wireless|2\.4G|2.4 GHz|Receiver|Unifying|Bolt|Logitech|Razer|Mouse|Keyboard|Controller|Headset|Headphone|Buds|WH-)')
              )
            }

            $result = foreach ($device in $devices) {
              $properties = [ordered]@{
                'System.ItemNameDisplay' = [string]$device.FriendlyName
                'DEVPKEY_NAME' = [string]$device.FriendlyName
                'DEVPKEY_Device_Manufacturer' = [string]$device.Manufacturer
                'DEVPKEY_Device_BusReportedDeviceDesc' = [string]$device.Description
              }

              [pscustomobject]@{
                InstanceId = $device.InstanceId
                Class = $device.Class
                FriendlyName = $device.FriendlyName
                Status = $device.Status
                Properties = $properties
              }
            }

            $result | ConvertTo-Json -Depth 6 -Compress
            """;

    private const string BatteryPropertyScript = """
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $ErrorActionPreference = 'SilentlyContinue'
            $instanceId = $env:BLUETOOTH_BATTERY_INSTANCE_ID
            $propertyKeys = @(
              'System.Devices.ContainerId',
              'System.Devices.FriendlyName',
              'System.Devices.ModelName',
              'System.Devices.BatteryLife',
              'System.Devices.BatteryPlusCharging',
              'DEVPKEY_Device_ContainerId',
              'DEVPKEY_Device_FriendlyName',
              'DEVPKEY_Device_BusReportedDeviceDesc',
              'DEVPKEY_Device_BatteryLife',
              'DEVPKEY_Device_BatteryLevel'
            )

            $properties = [ordered]@{}
            foreach ($key in $propertyKeys) {
              try {
                $property = Get-PnpDeviceProperty -InstanceId $instanceId -KeyName $key -ErrorAction Stop
                if ($null -ne $property.Data) {
                  $properties[$key] = [string]$property.Data
                }
              } catch {
              }
            }

            $properties | ConvertTo-Json -Depth 4 -Compress
            """;

    private static async Task<IReadOnlyList<RawPnpDevice>> EnrichBatteryPropertiesAsync(
        IReadOnlyList<RawPnpDevice> devices,
        CancellationToken cancellationToken)
    {
        var result = new List<RawPnpDevice>(devices.Count);
        var interestingDevices = devices
            .Where(IsBatteryProbeCandidate)
            .Take(60)
            .ToDictionary(device => device.InstanceId, StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices)
        {
            if (!interestingDevices.ContainsKey(device.InstanceId))
            {
                result.Add(device);
                continue;
            }

            using var perDeviceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perDeviceTimeout.CancelAfter(TimeSpan.FromSeconds(2));

            try
            {
                var stdout = await RunPowerShellAsync(
                    BatteryPropertyScript,
                    new Dictionary<string, string>
                    {
                        ["BLUETOOTH_BATTERY_INSTANCE_ID"] = device.InstanceId
                    },
                    perDeviceTimeout.Token).ConfigureAwait(false);

                var properties = DeserializeProperties(stdout);
                if (properties.Count == 0)
                {
                    result.Add(device);
                    continue;
                }

                var merged = new Dictionary<string, string?>(device.Properties, StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    merged[property.Key] = property.Value;
                }

                result.Add(device with { Properties = merged });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result.Add(device);
            }
            catch
            {
                result.Add(device);
            }
        }

        return result;
    }

    private static bool IsBatteryProbeCandidate(RawPnpDevice device)
    {
        var text = $"{device.InstanceId} {device.Class} {device.FriendlyName}";
        return text.Contains("BTH", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Battery", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("WH-", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Buds", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Receiver", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Unifying", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Bolt", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string?> DeserializeProperties(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string?>();
        }

        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            properties[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                ? null
                : property.Value.ToString();
        }

        return properties;
    }

    private static async Task<string> RunPowerShellAsync(
        string script,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        if (environment is not null)
        {
            foreach (var item in environment)
            {
                psi.Environment[item.Key] = item.Value;
            }
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        Task<string> stdoutTask;
        Task<string> stderrTask;
        try
        {
            stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"PowerShell device scan failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }

    private static IReadOnlyList<RawPnpDevice> DeserializeRawDevices(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return document.RootElement.EnumerateArray().Select(ReadRawDevice).ToArray();
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return [ReadRawDevice(document.RootElement)];
        }

        return [];
    }

    private static RawPnpDevice ReadRawDevice(JsonElement element)
    {
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in props.EnumerateObject())
            {
                properties[property.Name] = property.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : property.Value.ToString();
            }
        }

        return new RawPnpDevice
        {
            InstanceId = GetString(element, "InstanceId") ?? string.Empty,
            Class = GetString(element, "Class"),
            FriendlyName = GetString(element, "FriendlyName"),
            Status = GetString(element, "Status"),
            Properties = properties
        };
    }

    private static string GetStableGroupKey(RawPnpDevice device)
    {
        var address = DeviceIdentity.ExtractBluetoothAddress(device.InstanceId);
        if (!string.IsNullOrWhiteSpace(address))
        {
            return $"bt:{address}";
        }

        var containerId = GetProperty(device, "System.Devices.ContainerId") ??
                          GetProperty(device, "DEVPKEY_Device_ContainerId");
        if (!string.IsNullOrWhiteSpace(containerId))
        {
            return $"container:{containerId}";
        }

        var usbVidPid = DeviceIdentity.ExtractUsbVidPid(device.InstanceId);
        if (!string.IsNullOrWhiteSpace(usbVidPid))
        {
            return $"usb:{usbVidPid}";
        }

        return $"instance:{device.InstanceId}";
    }

    private static BluetoothDeviceSnapshot BuildSnapshot(
        string stableId,
        IReadOnlyList<RawPnpDevice> devices,
        DateTimeOffset now)
    {
        var address = devices
            .Select(device => DeviceIdentity.ExtractBluetoothAddress(device.InstanceId))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var containerId = devices
            .Select(device => GetProperty(device, "System.Devices.ContainerId") ?? GetProperty(device, "DEVPKEY_Device_ContainerId"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new BluetoothDeviceSnapshot
        {
            StableId = stableId,
            DisplayName = DeviceNameResolver.Resolve(devices),
            BluetoothAddress = address,
            ContainerId = containerId,
            Kind = ResolveKind(devices),
            IsConnected = ResolvePresence(stableId, devices) >= DevicePresence.LikelyActive,
            IsUserFacing = IsUserFacing(stableId, devices),
            Presence = ResolvePresence(stableId, devices),
            Evidence = ResolveEvidence(stableId, devices),
            Battery = ReadBattery(stableId, devices),
            LastSeenAt = now,
            Interfaces = devices
        };
    }

    private static DevicePresence ResolvePresence(string stableId, IReadOnlyList<RawPnpDevice> devices)
    {
        var name = DeviceNameResolver.Resolve(devices);
        var text = $"{stableId} {name} {string.Join(' ', devices.Select(device => device.InstanceId))}";

        if (text.Contains("Receiver", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("OMNI RECEIVER", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Unifying", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Bolt", StringComparison.OrdinalIgnoreCase))
        {
            return DevicePresence.ReceiverOnly;
        }

        if (stableId.StartsWith("usb:", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Razer", StringComparison.OrdinalIgnoreCase)))
        {
            return DevicePresence.LikelyActive;
        }

        if (stableId.StartsWith("bt:", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("ROG FALCHION", StringComparison.OrdinalIgnoreCase)))
        {
            return DevicePresence.LikelyActive;
        }

        if (IsSystemOrTransportNoiseName(name) || DeviceNameResolver.IsGenericName(name))
        {
            return DevicePresence.Noise;
        }

        return DevicePresence.PairedOnly;
    }

    private static string ResolveEvidence(string stableId, IReadOnlyList<RawPnpDevice> devices)
    {
        var presence = ResolvePresence(stableId, devices);
        return presence switch
        {
            DevicePresence.LikelyActive => "active heuristic: named input device",
            DevicePresence.ReceiverOnly => "receiver present; paired device activity not confirmed",
            DevicePresence.PairedOnly => "present in Windows device tree; live connection not confirmed",
            DevicePresence.Noise => "system, transport, or generic interface",
            DevicePresence.ConnectedConfirmed => "connection confirmed",
            _ => "unknown"
        };
    }

    private static BatteryReading? ReadBattery(string stableId, IReadOnlyList<RawPnpDevice> devices)
    {
        var context = new BatteryReadContext(
            stableId,
            DeviceNameResolver.Resolve(devices),
            devices.Select(device => DeviceIdentity.ExtractBluetoothAddress(device.InstanceId))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            devices);
        return BatteryProvider.TryReadAsync(context, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    private static bool IsUserFacing(string stableId, IReadOnlyList<RawPnpDevice> devices)
    {
        var name = DeviceNameResolver.Resolve(devices);
        if (IsSystemOrTransportNoiseName(name))
        {
            return false;
        }

        var presence = ResolvePresence(stableId, devices);
        if (presence >= DevicePresence.LikelyActive)
        {
            return true;
        }

        if (presence is DevicePresence.ReceiverOnly or DevicePresence.PairedOnly or DevicePresence.Noise)
        {
            return false;
        }

        return false;
    }

    private static bool IsSystemOrTransportNoiseName(string name)
    {
        return name.Contains("Microsoft 蓝牙", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Microsoft Bluetooth", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Realtek Bluetooth", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("RFCOMM Protocol", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("蓝牙 LE 通用属性服务", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("通用访问配置文件", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("通用属性配置文件", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("设备信息服务", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("设备标识服务", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("USB Composite Device", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("USB Input Device", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("USB 输入设备", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("通用 USB 集线器", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("通用 SuperSpeed USB 集线器", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveKind(IReadOnlyList<RawPnpDevice> devices)
    {
        var names = string.Join(' ', devices.Select(device => device.FriendlyName));
        if (names.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("Buds", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("WH-", StringComparison.OrdinalIgnoreCase))
        {
            return "Audio";
        }

        if (names.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("DualSense", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
        {
            return "Gamepad";
        }

        if (names.Contains("Mouse", StringComparison.OrdinalIgnoreCase))
        {
            return "Mouse";
        }

        if (names.Contains("Keyboard", StringComparison.OrdinalIgnoreCase))
        {
            return "Keyboard";
        }

        if (names.Contains("Receiver", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("Unifying", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("Bolt", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("2.4G", StringComparison.OrdinalIgnoreCase) ||
            names.Contains("2.4 GHz", StringComparison.OrdinalIgnoreCase))
        {
            return "2.4G Receiver";
        }

        if (devices.Any(device => string.Equals(device.Class, "AudioEndpoint", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(device.Class, "Media", StringComparison.OrdinalIgnoreCase)))
        {
            return "Audio";
        }

        if (devices.Any(device => string.Equals(device.Class, "HIDClass", StringComparison.OrdinalIgnoreCase)))
        {
            return "HID";
        }

        return "Bluetooth";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string? GetProperty(RawPnpDevice device, string key)
    {
        return device.Properties.TryGetValue(key, out var value) ? value : null;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }
}
