using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.Infrastructure.Execution;

public sealed class DefaultCheckEvaluator(ProcessInvoker processInvoker) : ICheckEvaluator
{
    public async Task<CheckEvaluationResult> EvaluateAsync(CheckDefinition check, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = check.Command,
            Arguments = check.Arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var commandResult = await processInvoker.RunAsync(startInfo, 15, waitForExit: true, cancellationToken);
        var outputParts = new[] { commandResult.StandardOutput, commandResult.StandardError }
            .Where(static value => !string.IsNullOrWhiteSpace(value));
        var output = string.Join(Environment.NewLine, outputParts);
        IReadOnlyCollection<int> exitCodes = check.SuccessExitCodes.Count == 0 ? new[] { 0 } : check.SuccessExitCodes;
        var exitMatches = commandResult.ExitCode.HasValue && exitCodes.Contains(commandResult.ExitCode.Value);
        var regexMatches = string.IsNullOrWhiteSpace(check.OutputRegex) || Regex.IsMatch(output, check.OutputRegex, RegexOptions.Multiline);
        var passed = exitMatches && regexMatches;
        if (check.Negate)
        {
            passed = !passed;
        }

        return new CheckEvaluationResult
        {
            Passed = passed,
            ExitCode = commandResult.ExitCode,
            Output = output,
            FailureReason = passed ? null : commandResult.FailureReason ?? "終了コードまたは正規表現が期待値に一致しません。"
        };
    }

    public async Task<PostCheckEvaluationResult> EvaluatePostCheckAsync(PostCheckDefinition definition, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        while (DateTimeOffset.Now - startedAt < TimeSpan.FromSeconds(Math.Max(1, definition.TimeoutSec)))
        {
            var passed = definition switch
            {
                ProcessExistsPostCheckDefinition processExists => EvaluateProcessExists(processExists),
                CommandCheckPostCheckDefinition commandCheck => (await EvaluateAsync(commandCheck.Check, cancellationToken)).Passed,
                ServiceStatePostCheckDefinition serviceState => EvaluateServiceState(serviceState),
                _ => false
            };

            if (passed)
            {
                return new PostCheckEvaluationResult { Passed = true };
            }

            await Task.Delay(Math.Max(100, definition.PollIntervalMs), cancellationToken);
        }

        return new PostCheckEvaluationResult
        {
            Passed = false,
            FailureReason = "事後確認がタイムアウトしました。"
        };
    }

    private static bool EvaluateProcessExists(ProcessExistsPostCheckDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ProcessName))
        {
            return false;
        }

        return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(definition.ProcessName)).Length > 0;
    }

    private static bool EvaluateServiceState(ServiceStatePostCheckDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ServiceName))
        {
            return false;
        }

        using var controller = new ServiceController(definition.ServiceName);
        return definition.ExpectedState switch
        {
            ServiceStateExpectation.Running => controller.Status == ServiceControllerStatus.Running,
            ServiceStateExpectation.Stopped => controller.Status == ServiceControllerStatus.Stopped,
            _ => false
        };
    }
}
