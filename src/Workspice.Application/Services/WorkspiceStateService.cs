using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.Application.Services;

public sealed class WorkspiceStateService(IProfileRepository repository) : IWorkspiceState
{
    public AppSettings Settings { get; private set; } = new();
    public AppState State { get; private set; } = AppState.NoActiveProfile();

    public ProfileDefinition? CurrentProfile => FindProfile(State.GetStableProfileId());

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Settings = await repository.LoadAsync(cancellationToken);
        State = string.IsNullOrWhiteSpace(Settings.LastActiveProfileId) || FindProfile(Settings.LastActiveProfileId) is null
            ? AppState.NoActiveProfile()
            : AppState.ActiveProfile(Settings.LastActiveProfileId);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return repository.SaveAsync(Settings, cancellationToken);
    }

    public ProfileDefinition? FindProfile(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return Settings.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }

    public void SetState(AppState state)
    {
        State = state;
    }

    public void ReplaceSettings(AppSettings settings)
    {
        Settings = settings;
        State = string.IsNullOrWhiteSpace(settings.LastActiveProfileId) || FindProfile(settings.LastActiveProfileId) is null
            ? AppState.NoActiveProfile()
            : AppState.ActiveProfile(settings.LastActiveProfileId);
    }

    public void SetLastActiveProfile(string? profileId)
    {
        Settings.LastActiveProfileId = profileId;
    }
}
