namespace Workspice.Infrastructure.Persistence;

public sealed class WorkspicePathOptions
{
    public string SettingsPath { get; init; } = string.Empty;
    public string LogsDirectory { get; init; } = string.Empty;
    public string WallpapersDirectory { get; init; } = string.Empty;

    public static WorkspicePathOptions CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new WorkspicePathOptions
        {
            SettingsPath = Path.Combine(appData, "Workspice", "profiles.json"),
            LogsDirectory = Path.Combine(localAppData, "Workspice", "logs"),
            WallpapersDirectory = Path.Combine(localAppData, "Workspice", "wallpapers")
        };
    }
}
