using System.Diagnostics;
using Workspice.Domain.Models;

namespace Workspice.Infrastructure.Execution;

internal sealed class ProcessInvoker
{
    public async Task<ExternalCommandResult> RunAsync(
        ProcessStartInfo startInfo,
        int timeoutSec,
        bool waitForExit,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ExternalCommandResult
            {
                Succeeded = false,
                FailureReason = ex.Message
            };
        }

        if (!waitForExit)
        {
            return new ExternalCommandResult
            {
                Succeeded = true,
                ExitCode = null
            };
        }

        var stdOutTask = process.StartInfo.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
        var stdErrTask = process.StartInfo.RedirectStandardError ? process.StandardError.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, timeoutSec)), cancellationToken);
        var completed = await Task.WhenAny(exitTask, timeoutTask);

        if (completed == timeoutTask)
        {
            TryKill(process);
            return new ExternalCommandResult
            {
                Succeeded = false,
                TimedOut = true,
                FailureReason = "タイムアウトしました。"
            };
        }

        await exitTask;
        var standardOutput = await stdOutTask;
        var standardError = await stdErrTask;

        return new ExternalCommandResult
        {
            Succeeded = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            FailureReason = process.ExitCode == 0 ? null : $"終了コード {process.ExitCode}"
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }
    }
}
