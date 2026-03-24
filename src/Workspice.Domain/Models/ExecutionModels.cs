namespace Workspice.Domain.Models;

public sealed class ExternalCommandResult
{
    public bool Succeeded { get; set; }
    public bool TimedOut { get; set; }
    public bool Cancelled { get; set; }
    public int? ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}

public sealed class CheckEvaluationResult
{
    public bool Passed { get; set; }
    public int? ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}

public sealed class PostCheckEvaluationResult
{
    public bool Passed { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class UserDecisionRecord
{
    public UserDecisionKind Kind { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

public sealed class ActionExecutionResult
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public ActionType ActionType { get; set; }
    public ActionExecutionStatus Status { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public int? ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public List<UserDecisionRecord> Decisions { get; set; } = [];
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public bool ContinuedAfterFailure { get; set; }
}

public sealed class SwitchExecutionResult
{
    public SwitchExecutionStatus Status { get; set; }
    public string? FromProfileId { get; set; }
    public string TargetProfileId { get; set; } = string.Empty;
    public AppState FinalState { get; set; } = AppState.NoActiveProfile();
    public List<ActionExecutionResult> Actions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<UserDecisionRecord> Decisions { get; set; } = [];
    public string? FailureReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
}
