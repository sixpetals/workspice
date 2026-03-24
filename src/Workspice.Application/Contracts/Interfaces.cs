using Workspice.Domain.Models;

namespace Workspice.Application.Contracts;

public interface IProfileRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IActionRunner
{
    Task<ExternalCommandResult> RunAsync(ActionDefinition action, CancellationToken cancellationToken = default);
}

public interface ICheckEvaluator
{
    Task<CheckEvaluationResult> EvaluateAsync(CheckDefinition check, CancellationToken cancellationToken = default);
    Task<PostCheckEvaluationResult> EvaluatePostCheckAsync(PostCheckDefinition definition, CancellationToken cancellationToken = default);
}

public interface IWallpaperService
{
    Task<string?> ApplyAsync(ProfileDefinition profile, CancellationToken cancellationToken = default);
}

public interface IExecutionLogService
{
    Task WriteAsync(SwitchExecutionResult result, int retentionDays, CancellationToken cancellationToken = default);
}

public interface IAutoStartService
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}

public interface IUserInteractionService
{
    Task<bool> ConfirmSwitchAsync(ProfileDefinition? currentProfile, ProfileDefinition targetProfile, CancellationToken cancellationToken = default);
    Task<ActionPromptDecision> PromptActionAsync(ActionDefinition action, CancellationToken cancellationToken = default);
    Task<FailureResolution> ResolveFailureAsync(ActionDefinition action, ActionExecutionResult executionResult, CancellationToken cancellationToken = default);
    Task ShowErrorAsync(string message, CancellationToken cancellationToken = default);
}

public interface IWorkspiceState
{
    AppSettings Settings { get; }
    AppState State { get; }
    ProfileDefinition? CurrentProfile { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    ProfileDefinition? FindProfile(string? profileId);
    void SetState(AppState state);
    void ReplaceSettings(AppSettings settings);
    void SetLastActiveProfile(string? profileId);
}

public interface ISwitchOrchestrator
{
    Task<SwitchExecutionResult> SwitchAsync(string targetProfileId, CancellationToken cancellationToken = default);
}
