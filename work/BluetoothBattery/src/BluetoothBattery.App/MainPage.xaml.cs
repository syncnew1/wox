using BluetoothBattery.Core.Configuration;
using BluetoothBattery.Core.Battery;
using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;
using BluetoothBattery.Core.Reporting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BluetoothBattery_App;

public sealed partial class MainPage : Page
{
    private readonly PnpDeviceScanner _scanner = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private bool _isRefreshing;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;

        _refreshTimer.Interval = TimeSpan.FromMinutes(2);
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshSwitch.IsOn)
        {
            _refreshTimer.Start();
        }

        await RefreshAsync();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        await RefreshAsync();
    }

    private void AutoRefreshSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshSwitch.IsOn)
        {
            _refreshTimer.Start();
            StatusText.Text = "自动刷新已开启：每 2 分钟只读刷新一次电量。";
        }
        else
        {
            _refreshTimer.Stop();
            StatusText.Text = "自动刷新已关闭。";
        }
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        RefreshButton.IsEnabled = false;
        RefreshProgress.IsActive = true;
        RefreshProgress.Visibility = Visibility.Visible;
        StatusText.Text = "正在只读扫描设备电量...";

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var devices = await _scanner.ScanAllAsync(
                deepScan: false,
                cancellationToken: timeout.Token);

            var configPath = GetConfigPath();
            var config = DeviceConfigLoader.LoadOrDefault(configPath);
            devices = new DeviceConfigApplier(config).Apply(devices);
            devices = devices
                .Where(device => device.Presence >= DevicePresence.LikelyActive)
                .ToArray();
            devices = await new DeviceBatteryEnricher().EnrichAsync(devices, timeout.Token);

            var summary = DeviceSummary.Create(devices);
            DeviceCountText.Text = summary.Total.ToString();
            ConnectedCountText.Text = summary.Connected.ToString();
            BatteryCountText.Text = summary.WithBattery.ToString();
            DeviceList.ItemsSource = devices.Select(DeviceRow.FromSnapshot).ToArray();

            LastRefreshText.Text = $"上次刷新：{DateTime.Now:T}";
            StatusText.Text = $"只读模式 · 配置：{configPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "刷新超时：设备可能正在休眠或被系统驱动占用。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"扫描失败：{ex.Message}";
        }
        finally
        {
            RefreshProgress.IsActive = false;
            RefreshProgress.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
            _isRefreshing = false;
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
    double BatteryValue,
    string BatterySource,
    string BatteryMeta,
    string StableId)
{
    public static DeviceRow FromSnapshot(BluetoothBattery.Core.Models.BluetoothDeviceSnapshot device)
    {
        var battery = device.Battery is null
            ? "未知"
            : $"{device.Battery.Percentage}%";
        var value = device.Battery?.Percentage ?? 0;
        var source = device.Battery is null
            ? "未找到可用电量来源"
            : device.Battery.Source;
        var meta = device.Battery is null
            ? TranslatePresence(device.Presence)
            : $"{TranslateConfidence(device.Battery.Confidence)} · {device.Battery.ReadAt:HH:mm:ss}";

        return new DeviceRow(
            device.DisplayName,
            device.Kind,
            battery,
            value,
            source,
            meta,
            device.StableId);
    }

    private static string TranslatePresence(DevicePresence presence)
    {
        return presence switch
        {
            DevicePresence.ConnectedConfirmed => "连接已确认",
            DevicePresence.LikelyActive => "疑似在线",
            DevicePresence.ReceiverOnly => "仅发现接收器",
            DevicePresence.PairedOnly => "仅配对记录",
            DevicePresence.Noise => "系统接口",
            _ => presence.ToString()
        };
    }

    private static string TranslateConfidence(BluetoothBattery.Core.Models.BatteryConfidence confidence)
    {
        return confidence switch
        {
            BluetoothBattery.Core.Models.BatteryConfidence.High => "高可信",
            BluetoothBattery.Core.Models.BatteryConfidence.Medium => "中可信",
            BluetoothBattery.Core.Models.BatteryConfidence.Cached => "缓存",
            BluetoothBattery.Core.Models.BatteryConfidence.Unknown => "未知可信度",
            _ => confidence.ToString()
        };
    }
}
