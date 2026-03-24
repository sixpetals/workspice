namespace Workspice.Domain.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<ProfileDefinition> Profiles { get; set; } = [];
    public string? LastActiveProfileId { get; set; }
    public bool StartWithWindows { get; set; }
    public int LogRetentionDays { get; set; } = 30;
}
