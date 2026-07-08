using System.Diagnostics;
using System.Text;

namespace BluetoothBattery.Core.Windows;

internal static class PowerShellRunner
{
    public static async Task<string> RunAsync(
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
            throw new InvalidOperationException($"PowerShell failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout;
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
}
