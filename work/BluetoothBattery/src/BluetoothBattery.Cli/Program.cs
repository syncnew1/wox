using BluetoothBattery.Core.Battery;
using BluetoothBattery.Core.Configuration;
using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;
using BluetoothBattery.Core.Reporting;

var outputJson = GetOptionValue(args, "--json");
var configPath = GetOptionValue(args, "--config") ?? Path.Combine("work", "BluetoothBattery", "config", "devices.json");
var timeoutSeconds = GetIntOptionValue(args, "--timeout-seconds", 30);
var bleBatteryAddress = GetOptionValue(args, "--ble-battery");
var raw = args.Any(arg => string.Equals(arg, "--raw", StringComparison.OrdinalIgnoreCase));
var deep = args.Any(arg => string.Equals(arg, "--deep", StringComparison.OrdinalIgnoreCase));
var all = args.Any(arg => string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase));
var connectedOnly = args.Any(arg => string.Equals(arg, "--connected-only", StringComparison.OrdinalIgnoreCase));
var summaryOnly = args.Any(arg => string.Equals(arg, "--summary", StringComparison.OrdinalIgnoreCase));
var writeSampleConfig = args.Any(arg => string.Equals(arg, "--write-sample-config", StringComparison.OrdinalIgnoreCase));
var help = args.Any(arg => arg is "-h" or "--help");

if (help)
{
    PrintHelp();
    return 0;
}

try
{
    if (writeSampleConfig)
    {
        DeviceConfigLoader.WriteSample(configPath);
        Console.WriteLine($"Sample config written to: {Path.GetFullPath(configPath)}");
        return 0;
    }

    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

    if (!string.IsNullOrWhiteSpace(bleBatteryAddress))
    {
        var provider = new BleGattBatteryProvider();
        var reading = await provider.TryReadAsync(
            new BatteryReadContext(
                $"bt:{bleBatteryAddress}",
                bleBatteryAddress,
                bleBatteryAddress,
                Array.Empty<RawPnpDevice>()),
            timeout.Token);

        if (reading is null)
        {
            Console.WriteLine($"No BLE GATT battery reading from {bleBatteryAddress}.");
            return 2;
        }

        Console.WriteLine($"{reading.Percentage}% ({reading.Source}, {reading.Confidence})");
        return 0;
    }

    Console.Error.WriteLine(deep
        ? "Deep scanning Windows wireless devices and battery properties..."
        : "Fast scanning Windows wireless devices (Bluetooth + 2.4G HID)...");
    Console.Error.WriteLine(deep
        ? "Deep mode can be slow on some systems. Omit -Deep if it times out."
        : "Use -Deep later to try slower Windows battery property reads.");

    var scanner = new PnpDeviceScanner();
    var devices = all || connectedOnly
        ? await scanner.ScanAllAsync(deep, timeout.Token)
        : await scanner.ScanAsync(deep, connectedOnly, timeout.Token);
    var config = DeviceConfigLoader.LoadOrDefault(configPath);
    devices = new DeviceConfigApplier(config).Apply(devices);
    if (connectedOnly)
    {
        devices = devices
            .Where(device => device.Presence >= BluetoothBattery.Core.Models.DevicePresence.LikelyActive)
            .ToArray();
    }
    devices = await new DeviceBatteryEnricher().EnrichAsync(devices, timeout.Token);

    if (raw)
    {
        Console.WriteLine(PnpDeviceScanner.ToJson(devices));
    }
    else if (summaryOnly)
    {
        PrintSummary(devices);
    }
    else
    {
        PrintSummary(devices);
        Console.WriteLine();
        PrintTable(devices);
    }

    if (!string.IsNullOrWhiteSpace(outputJson))
    {
        var fullPath = Path.GetFullPath(outputJson);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        await File.WriteAllTextAsync(fullPath, PnpDeviceScanner.ToJson(devices));
        Console.WriteLine();
        Console.WriteLine($"Diagnostics written to: {fullPath}");
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine($"Wireless device scan timed out after {timeoutSeconds} seconds.");
    Console.Error.WriteLine("Run without -Deep for fast device listing, or increase -TimeoutSeconds.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Wireless device scan failed.");
    Console.Error.WriteLine(ex.Message);
    if (ex.Message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("HRESULT 0x80041003", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("This Windows device query needs higher permission on your machine.");
        Console.Error.WriteLine("Open PowerShell as Administrator and run the same command again.");
    }

    return 1;
}

static string? GetOptionValue(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static int GetIntOptionValue(string[] args, string name, int defaultValue)
{
    var value = GetOptionValue(args, name);
    return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
}

static void PrintTable(IReadOnlyList<BluetoothBattery.Core.Models.BluetoothDeviceSnapshot> devices)
{
    if (devices.Count == 0)
    {
        Console.WriteLine("No present wireless-related devices found.");
        return;
    }

    foreach (var device in devices)
    {
        var battery = device.Battery is null
            ? "battery unavailable"
            : $"{device.Battery.Percentage}% ({device.Battery.Source})";

        Console.WriteLine(device.DisplayName);
        Console.WriteLine($"  Kind:       {device.Kind}");
        Console.WriteLine($"  Connected:  {device.IsConnected}");
        Console.WriteLine($"  Presence:   {device.Presence}");
        Console.WriteLine($"  Battery:    {battery}");
        Console.WriteLine($"  Visible:    {device.IsUserFacing}");
        Console.WriteLine($"  Stable ID:  {device.StableId}");
        Console.WriteLine($"  Interfaces: {device.Interfaces.Count}");
        Console.WriteLine();
    }
}

static void PrintSummary(IReadOnlyList<BluetoothBattery.Core.Models.BluetoothDeviceSnapshot> devices)
{
    var summary = DeviceSummary.Create(devices);
    Console.WriteLine("Wireless Battery MVP");
    Console.WriteLine("====================");
    Console.WriteLine($"Devices:     {summary.Total}");
    Console.WriteLine($"Connected:   {summary.Connected}");
    Console.WriteLine($"Battery:     {summary.WithBattery} available, {summary.WithoutBattery} unavailable");
    if (summary.ByKind.Count > 0)
    {
        Console.WriteLine("Kinds:       " + string.Join(", ", summary.ByKind.Select(item => $"{item.Key}={item.Value}")));
    }
}

static void PrintHelp()
{
    Console.WriteLine("BluetoothBattery.Cli");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  BluetoothBattery.Cli [--json <path>] [--raw]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --json <path>  Write a diagnostic JSON file.");
    Console.WriteLine("  --config <path>");
    Console.WriteLine("                 Apply aliases, hidden devices, and kind overrides.");
    Console.WriteLine("  --write-sample-config");
    Console.WriteLine("                 Write a starter device config to --config path.");
    Console.WriteLine("  --summary      Print only the summary counts.");
    Console.WriteLine("  --raw          Print the full diagnostic JSON instead of a table.");
    Console.WriteLine("  --all          Include hidden low-level interfaces and transport devices.");
    Console.WriteLine("  --connected-only");
    Console.WriteLine("                 Show only high-confidence active devices.");
    Console.WriteLine("  --deep         Also query slow Windows PnP battery properties.");
    Console.WriteLine("  --ble-battery <Bluetooth address>");
    Console.WriteLine("                 Directly read standard BLE GATT battery level.");
    Console.WriteLine("  --timeout-seconds <n>");
    Console.WriteLine("                 Stop scanning after n seconds. Default: 30.");
}
