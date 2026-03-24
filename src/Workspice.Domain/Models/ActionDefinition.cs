using System.Text.Json.Serialization;

namespace Workspice.Domain.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ApplicationLaunchActionDefinition), "applicationLaunch")]
[JsonDerivedType(typeof(CommandExecutionActionDefinition), "commandExecution")]
[JsonDerivedType(typeof(WindowsServiceControlActionDefinition), "windowsServiceControl")]
public abstract class ActionDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public abstract ActionType ActionType { get; }
    public int TimeoutSec { get; set; } = 30;
    public bool PromptBeforeRun { get; set; }
    public bool Enabled { get; set; } = true;
    public List<CheckDefinition> Preconditions { get; set; } = [];
    public PostCheckDefinition? PostCheck { get; set; }

    public abstract string DescribeTarget();
    public abstract string DescribeArguments();
}

public sealed class ApplicationLaunchActionDefinition : ActionDefinition
{
    public override ActionType ActionType => ActionType.ApplicationLaunch;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public RunLevel RunLevel { get; set; } = RunLevel.Elevated;

    public override string DescribeTarget() => ExecutablePath;
    public override string DescribeArguments() => Arguments;
}

public sealed class CommandExecutionActionDefinition : ActionDefinition
{
    public override ActionType ActionType => ActionType.CommandExecution;
    public string FileName { get; set; } = "cmd.exe";
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public RunLevel RunLevel { get; set; } = RunLevel.Elevated;

    public override string DescribeTarget() => FileName;
    public override string DescribeArguments() => Arguments;
}

public sealed class WindowsServiceControlActionDefinition : ActionDefinition
{
    public override ActionType ActionType => ActionType.WindowsServiceControl;
    public string ServiceName { get; set; } = string.Empty;
    public WindowsServiceOperation Operation { get; set; } = WindowsServiceOperation.Start;

    public override string DescribeTarget() => ServiceName;
    public override string DescribeArguments() => Operation.ToString();
}
