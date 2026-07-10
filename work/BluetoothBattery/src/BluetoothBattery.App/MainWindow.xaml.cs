using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BluetoothBattery.Core.Battery;
using BluetoothBattery.Core.Configuration;
using BluetoothBattery.Core.Models;
using BluetoothBattery.Core.Pnp;
using BluetoothBattery.Core.Reporting;

namespace BluetoothBattery_App;

public partial class MainWindow : Window
{
    private readonly PnpDeviceScanner _scanner = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private bool _isRefreshing;

    public ObservableCollection<DeviceRow> Devices { get; } = new();

    public MainWindow()
    {
        AppLogger.Info("MainWindow constructor started.");
        InitializeComponent();
        DataContext = this;

        _refreshTimer.Interval = TimeSpan.FromMinutes(2);
        _refreshTimer.Tick += RefreshTimer_Tick;

        AppLogger.Info("MainWindow constructor completed.");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheckBox.IsChecked == true)
        {
            _refreshTimer.Start();
        }

        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private void AutoRefreshCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (StatusText is null)
        {
            return;
        }

        if (AutoRefreshCheckBox.IsChecked == true)
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

            Devices.Clear();
            foreach (var row in devices.Select(DeviceRow.FromSnapshot))
            {
                Devices.Add(row);
            }

            EmptyStateText.Visibility = Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LastRefreshText.Text = $"上次刷新：{DateTime.Now:T}";
            StatusText.Text = $"只读模式 · 配置：{configPath}";
            AppLogger.Info($"Refresh completed. Devices={summary.Total}, Connected={summary.Connected}, WithBattery={summary.WithBattery}.");
        }
        catch (OperationCanceledException)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            StatusText.Text = "刷新超时：设备可能正在休眠或被系统驱动占用。";
            AppLogger.Info("Refresh timed out.");
        }
        catch (Exception ex)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            StatusText.Text = $"扫描失败：{ex.Message}";
            AppLogger.Error("Refresh failed.", ex);
        }
        finally
        {
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
    string BatterySource,
    string BatteryMeta,
    string StableId)
{
    public static DeviceRow FromSnapshot(BluetoothDeviceSnapshot device)
    {
        var battery = device.Battery is null
            ? "未知"
            : $"{device.Battery.Percentage}%";
        var source = device.Battery is null
            ? "未找到可用电量来源"
            : device.Battery.Source;
        var meta = device.Battery is null
            ? TranslatePresence(device.Presence)
            : $"{TranslateBatteryStatus(device.Battery.Percentage)} · {TranslateConfidence(device.Battery.Confidence)} · {device.Battery.ReadAt:HH:mm:ss}";

        return new DeviceRow(
            device.DisplayName,
            device.Kind,
            battery,
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

    private static string TranslateConfidence(BatteryConfidence confidence)
    {
        return confidence switch
        {
            BatteryConfidence.High => "高可信",
            BatteryConfidence.Medium => "中可信",
            BatteryConfidence.Cached => "缓存",
            BatteryConfidence.Unknown => "未知可信度",
            _ => confidence.ToString()
        };
    }

    private static string TranslateBatteryStatus(int percentage)
    {
        return percentage switch
        {
            <= 10 => "电量极低",
            <= 25 => "电量偏低",
            >= 95 => "接近满电",
            _ => "电量正常"
        };
    }
}
