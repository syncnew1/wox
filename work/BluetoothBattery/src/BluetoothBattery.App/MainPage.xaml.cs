using BluetoothBattery.Core.Configuration;
using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;
using BluetoothBattery.Core.Reporting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BluetoothBattery_App;

public sealed partial class MainPage : Page
{
    private readonly PnpDeviceScanner _scanner = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        StatusText.Text = "\u6b63\u5728\u626b\u63cf\u8bbe\u5907...";

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var devices = await _scanner.ScanAllAsync(
                deepScan: false,
                cancellationToken: timeout.Token);

            var configPath = GetConfigPath();
            var config = DeviceConfigLoader.LoadOrDefault(configPath);
            devices = new DeviceConfigApplier(config).Apply(devices);
            devices = devices
                .Where(device => device.Presence >= DevicePresence.LikelyActive)
                .ToArray();

            var summary = DeviceSummary.Create(devices);
            DeviceCountText.Text = summary.Total.ToString();
            ConnectedCountText.Text = summary.Connected.ToString();
            BatteryCountText.Text = summary.WithBattery.ToString();
            DeviceList.ItemsSource = devices.Select(DeviceRow.FromSnapshot).ToArray();

            StatusText.Text = $"\u4e0a\u6b21\u5237\u65b0\uff1a{DateTime.Now:t} \u00b7 \u914d\u7f6e\uff1a{configPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"\u626b\u63cf\u5931\u8d25\uff1a{ex.Message}";
        }
    }

    private static string GetConfigPath()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "work", "BluetoothBattery", "config", "devices.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(current.FullName, "config", "devices.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine("work", "BluetoothBattery", "config", "devices.json"));
    }
}

public sealed record DeviceRow(
    string Name,
    string Kind,
    string Battery,
    string Presence,
    string StableId)
{
    public static DeviceRow FromSnapshot(BluetoothBattery.Core.Models.BluetoothDeviceSnapshot device)
    {
        var battery = device.Battery is null
            ? "\u672a\u77e5"
            : $"{device.Battery.Percentage}%";

        return new DeviceRow(
            device.DisplayName,
            device.Kind,
            battery,
            device.Presence.ToString(),
            device.StableId);
    }
}
