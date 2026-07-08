using System.Globalization;
using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Windows;

namespace BluetoothBattery.Core.Battery;

public sealed class BleGattBatteryProvider : IBatteryProvider
{
    private const string BatteryServiceUuid = "0000180f-0000-1000-8000-00805f9b34fb";
    private const string BatteryLevelCharacteristicUuid = "00002a19-0000-1000-8000-00805f9b34fb";

    public string Name => "BLE GATT Battery Service";

    public int Priority => 10;

    public async ValueTask<BatteryReading?> TryReadAsync(
        BatteryReadContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.BluetoothAddress))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));

        try
        {
            var stdout = await PowerShellRunner.RunAsync(
                BleBatteryScript,
                new Dictionary<string, string>
                {
                    ["BLUETOOTH_BATTERY_ADDRESS"] = context.BluetoothAddress
                },
                timeout.Token).ConfigureAwait(false);

            if (!int.TryParse(stdout.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percentage))
            {
                return null;
            }

            if (percentage is < 0 or > 100)
            {
                return null;
            }

            return new BatteryReading(
                percentage,
                Name,
                BatteryConfidence.High,
                DateTimeOffset.Now);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private const string BleBatteryScript = $$"""
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $ErrorActionPreference = 'Stop'

            $addressText = $env:BLUETOOTH_BATTERY_ADDRESS
            if ([string]::IsNullOrWhiteSpace($addressText)) {
              exit 0
            }

            $addressText = $addressText.Replace(':', '').Replace('-', '')
            $address = [Convert]::ToUInt64($addressText, 16)

            Add-Type -AssemblyName System.Runtime.WindowsRuntime
            [void][Windows.Devices.Bluetooth.BluetoothLEDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
            [void][Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
            [void][Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
            [void][Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult, Windows.Devices.Bluetooth, ContentType = WindowsRuntime]
            [void][Windows.Storage.Streams.DataReader, Windows.Foundation, ContentType = WindowsRuntime]

            function Await-WinRt($operation, [Type]$resultType) {
              $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
                Where-Object {
                  $_.Name -eq 'AsTask' -and
                  $_.IsGenericMethodDefinition -and
                  $_.GetParameters().Count -eq 1
                } |
                Select-Object -First 1
              $task = $method.MakeGenericMethod($resultType).Invoke($null, @($operation))
              return $task.GetAwaiter().GetResult()
            }

            $deviceOperation = [Windows.Devices.Bluetooth.BluetoothLEDevice]::FromBluetoothAddressAsync($address)
            $device = Await-WinRt $deviceOperation ([Windows.Devices.Bluetooth.BluetoothLEDevice])
            if ($null -eq $device) {
              exit 0
            }

            try {
              $serviceUuid = [Guid]'{{BatteryServiceUuid}}'
              $characteristicUuid = [Guid]'{{BatteryLevelCharacteristicUuid}}'

              $serviceOperation = $device.GetGattServicesForUuidAsync($serviceUuid)
              $serviceResult = Await-WinRt $serviceOperation ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult])
              if ($serviceResult.Status.ToString() -ne 'Success' -or $serviceResult.Services.Count -eq 0) {
                exit 0
              }

              $service = $serviceResult.Services[0]
              $characteristicOperation = $service.GetCharacteristicsForUuidAsync($characteristicUuid)
              $characteristicResult = Await-WinRt $characteristicOperation ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult])
              if ($characteristicResult.Status.ToString() -ne 'Success' -or $characteristicResult.Characteristics.Count -eq 0) {
                exit 0
              }

              $characteristic = $characteristicResult.Characteristics[0]
              $readOperation = $characteristic.ReadValueAsync()
              $readResult = Await-WinRt $readOperation ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult])
              if ($readResult.Status.ToString() -ne 'Success' -or $readResult.Value.Length -lt 1) {
                exit 0
              }

              $fromBuffer = [Windows.Storage.Streams.DataReader].GetMethods() |
                Where-Object { $_.Name -eq 'FromBuffer' } |
                Select-Object -First 1
              $reader = $fromBuffer.Invoke($null, @($readResult.Value))
              [string]$reader.ReadByte()
            }
            finally {
              if ($null -ne $device) {
                $device.Dispose()
              }
            }
            """;
}
