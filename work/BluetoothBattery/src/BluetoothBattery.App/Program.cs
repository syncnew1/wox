using System.Windows;

namespace BluetoothBattery_App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            AppLogger.Info("Program.Main started.");

            var app = new App();
            AppLogger.Info("App instance created.");

            var window = new MainWindow();
            AppLogger.Info("MainWindow instance created.");

            app.Run(window);
            AppLogger.Info("Application exited normally.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Application startup failed.", ex);
            MessageBox.Show(
                $"无线设备电量启动失败：{ex.Message}\n\n日志：{AppLogger.LogPath}\n发布目录日志：{AppLogger.LocalLogPath}",
                "无线设备电量",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
