using System.Diagnostics;
using Workspice.Application.Contracts;

namespace Workspice.Infrastructure.Startup;

public sealed class TaskSchedulerAutoStartService(string executablePath, string taskName = "Workspice") : IAutoStartService
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunSchtasksAsync($"/Query /TN \"{taskName}\"", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (enabled)
        {
            var command = $"/Create /F /SC ONLOGON /RL HIGHEST /TN \"{taskName}\" /TR \"\\\"{executablePath}\\\"\"";
            var result = await RunSchtasksAsync(command, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.StandardError.Length > 0 ? result.StandardError : result.StandardOutput);
            }

            return;
        }

        if (!await IsEnabledAsync(cancellationToken))
        {
            return;
        }

        var deleteResult = await RunSchtasksAsync($"/Delete /F /TN \"{taskName}\"", cancellationToken);
        if (deleteResult.ExitCode != 0)
        {
            throw new InvalidOperationException(deleteResult.StandardError.Length > 0 ? deleteResult.StandardError : deleteResult.StandardOutput);
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunSchtasksAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stdout, await stderr);
    }
}
