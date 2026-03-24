using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.Application.Services;

public sealed class SwitchOrchestrator(
    IWorkspiceState state,
    IActionRunner actionRunner,
    ICheckEvaluator checkEvaluator,
    IWallpaperService wallpaperService,
    IExecutionLogService executionLogService,
    IUserInteractionService userInteractionService) : ISwitchOrchestrator
{
    private readonly SemaphoreSlim _switchGate = new(1, 1);

    public async Task<SwitchExecutionResult> SwitchAsync(string targetProfileId, CancellationToken cancellationToken = default)
    {
        if (!await _switchGate.WaitAsync(0, cancellationToken))
        {
            await userInteractionService.ShowErrorAsync("切替処理の実行中は別の切替を開始できません。", cancellationToken);
            return new SwitchExecutionResult
            {
                Status = SwitchExecutionStatus.Rejected,
                TargetProfileId = targetProfileId,
                FinalState = state.State,
                FailureReason = "切替処理がすでに実行中です。",
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now
            };
        }

        try
        {
            var targetProfile = state.FindProfile(targetProfileId)
                ?? throw new InvalidOperationException($"Target profile '{targetProfileId}' was not found.");
            var currentProfile = state.CurrentProfile;

            var result = new SwitchExecutionResult
            {
                FromProfileId = currentProfile?.Id,
                TargetProfileId = targetProfileId,
                StartedAt = DateTimeOffset.Now,
                FinalState = state.State
            };

            var confirmed = await userInteractionService.ConfirmSwitchAsync(currentProfile, targetProfile, cancellationToken);
            result.Decisions.Add(new UserDecisionRecord
            {
                Kind = UserDecisionKind.SwitchConfirmation,
                Decision = confirmed ? "Execute" : "Cancel",
                Detail = $"{currentProfile?.Name ?? "(なし)"} -> {targetProfile.Name}"
            });

            if (!confirmed)
            {
                result.Status = SwitchExecutionStatus.Cancelled;
                result.FinalState = state.State;
                result.EndedAt = DateTimeOffset.Now;
                await executionLogService.WriteAsync(result, state.Settings.LogRetentionDays, cancellationToken);
                return result;
            }

            state.SetState(AppState.Transitioning(currentProfile?.Id, targetProfile.Id));

            if (currentProfile is not null)
            {
                var shutdownOutcome = await ExecuteActionSequenceAsync(
                    currentProfile.ShutdownActions,
                    targetProfile,
                    isLaunchPhase: false,
                    result,
                    cancellationToken);
                if (!shutdownOutcome)
                {
                    result.EndedAt = DateTimeOffset.Now;
                    await executionLogService.WriteAsync(result, state.Settings.LogRetentionDays, cancellationToken);
                    return result;
                }
            }

            var launchOutcome = await ExecuteActionSequenceAsync(
                targetProfile.LaunchActions,
                targetProfile,
                isLaunchPhase: true,
                result,
                cancellationToken);
            if (!launchOutcome)
            {
                result.EndedAt = DateTimeOffset.Now;
                await executionLogService.WriteAsync(result, state.Settings.LogRetentionDays, cancellationToken);
                return result;
            }

            try
            {
                await wallpaperService.ApplyAsync(targetProfile, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"壁紙の適用に失敗しました: {ex.Message}");
            }

            state.SetLastActiveProfile(targetProfile.Id);
            state.SetState(AppState.ActiveProfile(targetProfile.Id));
            await state.SaveAsync(cancellationToken);

            result.FinalState = state.State;
            result.Status = result.Actions.Any(action => action.ContinuedAfterFailure)
                ? SwitchExecutionStatus.CompletedWithWarnings
                : SwitchExecutionStatus.Succeeded;
            result.EndedAt = DateTimeOffset.Now;
            await executionLogService.WriteAsync(result, state.Settings.LogRetentionDays, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            var failure = new SwitchExecutionResult
            {
                Status = SwitchExecutionStatus.Failed,
                TargetProfileId = targetProfileId,
                FromProfileId = state.CurrentProfile?.Id,
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now,
                FinalState = state.State,
                FailureReason = ex.Message
            };
            await executionLogService.WriteAsync(failure, state.Settings.LogRetentionDays, cancellationToken);
            await userInteractionService.ShowErrorAsync(ex.Message, cancellationToken);
            return failure;
        }
        finally
        {
            _switchGate.Release();
        }
    }

    private async Task<bool> ExecuteActionSequenceAsync(
        IReadOnlyList<ActionDefinition> actions,
        ProfileDefinition targetProfile,
        bool isLaunchPhase,
        SwitchExecutionResult switchResult,
        CancellationToken cancellationToken)
    {
        foreach (var action in actions)
        {
            while (true)
            {
                var executionResult = await ExecuteActionAsync(action, cancellationToken);
                switchResult.Actions.Add(executionResult);

                if (executionResult.Status is ActionExecutionStatus.Succeeded or ActionExecutionStatus.Skipped)
                {
                    break;
                }

                if (executionResult.Status == ActionExecutionStatus.Cancelled)
                {
                    ApplyAbortState(isLaunchPhase, targetProfile.Id, switchResult, "ユーザにより中止されました。");
                    return false;
                }

                var resolution = await userInteractionService.ResolveFailureAsync(action, executionResult, cancellationToken);
                executionResult.Decisions.Add(new UserDecisionRecord
                {
                    Kind = UserDecisionKind.FailureResolution,
                    Decision = resolution.ToString(),
                    Detail = executionResult.FailureReason
                });

                if (resolution == FailureResolution.Retry)
                {
                    switchResult.Actions.Remove(executionResult);
                    continue;
                }

                if (resolution == FailureResolution.Continue)
                {
                    executionResult.ContinuedAfterFailure = true;
                    switchResult.Warnings.Add($"{action.Name}: {executionResult.FailureReason}");
                    break;
                }

                ApplyAbortState(isLaunchPhase, targetProfile.Id, switchResult, executionResult.FailureReason ?? "失敗により中止されました。");
                return false;
            }
        }

        return true;
    }

    private async Task<ActionExecutionResult> ExecuteActionAsync(ActionDefinition action, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var result = new ActionExecutionResult
        {
            ActionId = action.Id,
            ActionName = action.Name,
            ActionType = action.ActionType,
            Target = action.DescribeTarget(),
            Arguments = action.DescribeArguments(),
            StartedAt = startedAt
        };

        if (!action.Enabled)
        {
            result.Status = ActionExecutionStatus.Skipped;
            result.FailureReason = "無効化されたアクションです。";
            result.EndedAt = DateTimeOffset.Now;
            return result;
        }

        foreach (var check in action.Preconditions)
        {
            var checkResult = await checkEvaluator.EvaluateAsync(check, cancellationToken);
            if (!checkResult.Passed)
            {
                result.Status = ActionExecutionStatus.Skipped;
                result.FailureReason = $"事前条件チェック不一致: {checkResult.FailureReason ?? check.Describe()}";
                result.StandardOutput = checkResult.Output;
                result.ExitCode = checkResult.ExitCode;
                result.EndedAt = DateTimeOffset.Now;
                return result;
            }
        }

        if (action.PromptBeforeRun)
        {
            var promptDecision = await userInteractionService.PromptActionAsync(action, cancellationToken);
            result.Decisions.Add(new UserDecisionRecord
            {
                Kind = UserDecisionKind.ActionPrompt,
                Decision = promptDecision.ToString(),
                Detail = action.Name
            });

            if (promptDecision == ActionPromptDecision.Skip)
            {
                result.Status = ActionExecutionStatus.Skipped;
                result.FailureReason = "個別確認でスキップされました。";
                result.EndedAt = DateTimeOffset.Now;
                return result;
            }

            if (promptDecision == ActionPromptDecision.Abort)
            {
                result.Status = ActionExecutionStatus.Cancelled;
                result.FailureReason = "個別確認で中止されました。";
                result.EndedAt = DateTimeOffset.Now;
                return result;
            }
        }

        var runResult = await actionRunner.RunAsync(action, cancellationToken);
        result.ExitCode = runResult.ExitCode;
        result.StandardOutput = runResult.StandardOutput;
        result.StandardError = runResult.StandardError;
        result.FailureReason = runResult.FailureReason;

        if (runResult.Cancelled)
        {
            result.Status = ActionExecutionStatus.Cancelled;
            result.EndedAt = DateTimeOffset.Now;
            return result;
        }

        if (runResult.TimedOut)
        {
            result.Status = ActionExecutionStatus.TimedOut;
            result.EndedAt = DateTimeOffset.Now;
            return result;
        }

        if (!runResult.Succeeded)
        {
            result.Status = ActionExecutionStatus.Failed;
            result.EndedAt = DateTimeOffset.Now;
            return result;
        }

        if (action.PostCheck is not null)
        {
            var postCheck = await checkEvaluator.EvaluatePostCheckAsync(action.PostCheck, cancellationToken);
            if (!postCheck.Passed)
            {
                result.Status = ActionExecutionStatus.Failed;
                result.FailureReason = postCheck.FailureReason ?? "事後確認がタイムアウトしました。";
                result.EndedAt = DateTimeOffset.Now;
                return result;
            }
        }

        result.Status = ActionExecutionStatus.Succeeded;
        result.EndedAt = DateTimeOffset.Now;
        return result;
    }

    private void ApplyAbortState(bool isLaunchPhase, string targetProfileId, SwitchExecutionResult switchResult, string failureReason)
    {
        var lastStableId = state.State.GetStableProfileId();
        if (isLaunchPhase)
        {
            state.SetState(AppState.AttentionRequired(lastStableId, targetProfileId));
        }
        else
        {
            state.SetState(string.IsNullOrWhiteSpace(lastStableId) ? AppState.NoActiveProfile() : AppState.ActiveProfile(lastStableId));
        }

        switchResult.Status = SwitchExecutionStatus.Cancelled;
        switchResult.FailureReason = failureReason;
        switchResult.FinalState = state.State;
    }
}
