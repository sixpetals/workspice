namespace Workspice.Domain.Models;

public enum WallpaperMode
{
    CustomImage,
    GeneratedFromProfileName
}

public enum RunLevel
{
    Standard,
    Elevated
}

public enum ActionType
{
    ApplicationLaunch,
    CommandExecution,
    WindowsServiceControl
}

public enum WindowsServiceOperation
{
    Start,
    Stop
}

public enum ServiceStateExpectation
{
    Running,
    Stopped
}

public enum AppStateKind
{
    NoActiveProfile,
    ActiveProfile,
    Transitioning,
    AttentionRequired
}

public enum ActionExecutionStatus
{
    Succeeded,
    Failed,
    Skipped,
    TimedOut,
    Cancelled
}

public enum SwitchExecutionStatus
{
    Succeeded,
    CompletedWithWarnings,
    Cancelled,
    Rejected,
    Failed
}

public enum ActionPromptDecision
{
    Run,
    Skip,
    Abort
}

public enum FailureResolution
{
    Retry,
    Continue,
    Abort
}

public enum UserDecisionKind
{
    SwitchConfirmation,
    ActionPrompt,
    FailureResolution
}
