using System.Windows;

namespace BluetoothBattery_App;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        AppLogger.Info("WPF application constructor started.");
        AppLogger.Info("WPF application initialized.");
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Unhandled AppDomain exception.", ex);
        }
        else
        {
            AppLogger.Info($"Unhandled AppDomain exception object: {e.ExceptionObject}");
        }
    }

    private static void App_DispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled WPF dispatcher exception.", e.Exception);
    }
}
