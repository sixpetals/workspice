namespace Workspice.Domain.Models;

public sealed class CheckDefinition
{
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public List<int> SuccessExitCodes { get; set; } = [0];
    public string? OutputRegex { get; set; }
    public bool Negate { get; set; }

    public string Describe()
    {
        return string.IsNullOrWhiteSpace(Arguments) ? Command : $"{Command} {Arguments}";
    }
}
