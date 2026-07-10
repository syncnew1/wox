using System.IO;
using System.Text;

namespace BluetoothBattery_App;

internal static class AppLogger
{
    private static readonly object Lock = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BluetoothBattery",
        "app.log");

    public static string LocalLogPath { get; } = Path.Combine(AppContext.BaseDirectory, "app.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line, Encoding.UTF8);
                File.AppendAllText(LocalLogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            try
            {
                File.AppendAllText(LocalLogPath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
