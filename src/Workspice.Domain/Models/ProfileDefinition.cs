namespace Workspice.Domain.Models;

public sealed class ProfileDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WallpaperMode WallpaperMode { get; set; } = WallpaperMode.GeneratedFromProfileName;
    public string? WallpaperPath { get; set; }
    public List<ActionDefinition> LaunchActions { get; set; } = [];
    public List<ActionDefinition> ShutdownActions { get; set; } = [];

    public override string ToString() => Name;
}
