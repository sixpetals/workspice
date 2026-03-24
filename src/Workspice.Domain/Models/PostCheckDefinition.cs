using System.Text.Json.Serialization;

namespace Workspice.Domain.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ProcessExistsPostCheckDefinition), "processExists")]
[JsonDerivedType(typeof(CommandCheckPostCheckDefinition), "commandCheck")]
[JsonDerivedType(typeof(ServiceStatePostCheckDefinition), "serviceState")]
public abstract class PostCheckDefinition
{
    public int TimeoutSec { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 1000;
}

public sealed class ProcessExistsPostCheckDefinition : PostCheckDefinition
{
    public string ProcessName { get; set; } = string.Empty;
}

public sealed class CommandCheckPostCheckDefinition : PostCheckDefinition
{
    public CheckDefinition Check { get; set; } = new();
}

public sealed class ServiceStatePostCheckDefinition : PostCheckDefinition
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceStateExpectation ExpectedState { get; set; } = ServiceStateExpectation.Running;
}
