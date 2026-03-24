using System.Diagnostics;
using System.ServiceProcess;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.Infrastructure.Execution;

public sealed class DefaultActionRunner(ProcessInvoker processInvoker) : IActionRunner
{
    public async Task<ExternalCommandResult> RunAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        return action switch
        {
            ApplicationLaunchActionDefinition app => await RunApplicationLaunchAsync(app, cancellationToken),
            CommandExecutionActionDefinition command => await RunCommandExecutionAsync(command, cancellationToken),
            WindowsServiceControlActionDefinition service => await RunServiceActionAsync(service),
            _ => new ExternalCommandResult { Succeeded = false, FailureReason = $"未対応のアクション種別です: {action.ActionType}" }
        };
    }

    private Task<ExternalCommandResult> RunApplicationLaunchAsync(ApplicationLaunchActionDefinition action, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = action.ExecutablePath,
            Arguments = action.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? Environment.CurrentDirectory : action.WorkingDirectory,
            UseShellExecute = false
        };
        return processInvoker.RunAsync(startInfo, action.TimeoutSec, waitForExit: false, cancellationToken);
    }

    private Task<ExternalCommandResult> RunCommandExecutionAsync(CommandExecutionActionDefinition action, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = action.FileName,
            Arguments = action.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? Environment.CurrentDirectory : action.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        return processInvoker.RunAsync(startInfo, action.TimeoutSec, waitForExit: true, cancellationToken);
    }

    private static Task<ExternalCommandResult> RunServiceActionAsync(WindowsServiceControlActionDefinition action)
    {
        try
        {
            using var controller = new ServiceController(action.ServiceName);
            if (action.Operation == WindowsServiceOperation.Start && controller.Status != ServiceControllerStatus.Running)
            {
                controller.Start();
            }
            else if (action.Operation == WindowsServiceOperation.Stop && controller.Status != ServiceControllerStatus.Stopped)
            {
                controller.Stop();
            }

            return Task.FromResult(new ExternalCommandResult { Succeeded = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ExternalCommandResult
            {
                Succeeded = false,
                FailureReason = ex.Message
            });
        }
    }
}
