using Workspice.Application.Contracts;
using Workspice.Application.Services;
using Workspice.Domain.Models;
using Workspice.Infrastructure.Persistence;
using Xunit;

namespace Workspice.Tests;

public sealed class CoreTests
{
    [Fact]
    public async Task JsonProfileRepository_RoundTrips_Settings()
    {
        var root = CreateTempDirectory();
        var repository = new JsonProfileRepository(new WorkspicePathOptions
        {
            SettingsPath = Path.Combine(root, "profiles.json"),
            LogsDirectory = Path.Combine(root, "logs"),
            WallpapersDirectory = Path.Combine(root, "wallpapers")
        });

        var settings = new AppSettings
        {
            LastActiveProfileId = "profile-a",
            StartWithWindows = true,
            LogRetentionDays = 45,
            Profiles =
            [
                new ProfileDefinition
                {
                    Id = "profile-a",
                    Name = "開発A",
                    LaunchActions = [new CommandExecutionActionDefinition { Name = "run", FileName = "cmd.exe", Arguments = "/c echo hi" }]
                }
            ]
        };

        await repository.SaveAsync(settings);
        var loaded = await repository.LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal("profile-a", loaded.LastActiveProfileId);
        Assert.Single(loaded.Profiles);
        Assert.IsType<CommandExecutionActionDefinition>(loaded.Profiles[0].LaunchActions[0]);
    }

    [Fact]
    public async Task JsonProfileRepository_Rejects_Unknown_SchemaVersion()
    {
        var root = CreateTempDirectory();
        var settingsPath = Path.Combine(root, "profiles.json");
        await File.WriteAllTextAsync(settingsPath, """
        {
          "schemaVersion": 99,
          "profiles": []
        }
        """);

        var repository = new JsonProfileRepository(new WorkspicePathOptions
        {
            SettingsPath = settingsPath,
            LogsDirectory = Path.Combine(root, "logs"),
            WallpapersDirectory = Path.Combine(root, "wallpapers")
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.LoadAsync());
    }

    [Fact]
    public async Task SwitchOrchestrator_ContinuesAfterFailure_AndEndsActive()
    {
        var state = await CreateLoadedStateAsync();
        var runner = new FakeActionRunner(
            new ExternalCommandResult { Succeeded = true },
            new ExternalCommandResult { Succeeded = false, FailureReason = "launch failed" },
            new ExternalCommandResult { Succeeded = true });
        var interaction = new FakeUserInteractionService
        {
            ConfirmSwitch = true,
            FailureResolution = FailureResolution.Continue
        };
        var orchestrator = new SwitchOrchestrator(
            state,
            runner,
            new FakeCheckEvaluator(),
            new FakeWallpaperService(),
            new FakeExecutionLogService(),
            interaction);

        var result = await orchestrator.SwitchAsync("target");

        Assert.Equal(SwitchExecutionStatus.CompletedWithWarnings, result.Status);
        Assert.Equal(AppStateKind.ActiveProfile, result.FinalState.Kind);
        Assert.Equal("target", result.FinalState.ActiveProfileId);
        Assert.Equal(new[] { "shutdown-1", "launch-1", "launch-2" }, runner.ExecutedActionNames);
        Assert.Contains(result.Actions, action => action.ActionName == "launch-1" && action.ContinuedAfterFailure);
    }

    [Fact]
    public async Task SwitchOrchestrator_AbortDuringLaunch_EntersAttentionRequired()
    {
        var state = await CreateLoadedStateAsync();
        var runner = new FakeActionRunner(
            new ExternalCommandResult { Succeeded = true },
            new ExternalCommandResult { Succeeded = false, FailureReason = "launch failed" });
        var interaction = new FakeUserInteractionService
        {
            ConfirmSwitch = true,
            FailureResolution = FailureResolution.Abort
        };
        var orchestrator = new SwitchOrchestrator(
            state,
            runner,
            new FakeCheckEvaluator(),
            new FakeWallpaperService(),
            new FakeExecutionLogService(),
            interaction);

        var result = await orchestrator.SwitchAsync("target");

        Assert.Equal(SwitchExecutionStatus.Cancelled, result.Status);
        Assert.Equal(AppStateKind.AttentionRequired, result.FinalState.Kind);
        Assert.Equal("current", result.FinalState.LastStableProfileId);
        Assert.Equal("target", result.FinalState.FailedTargetProfileId);
    }

    [Fact]
    public async Task SwitchOrchestrator_Rejects_Concurrent_Switch()
    {
        var state = await CreateLoadedStateAsync();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new BlockingActionRunner(gate);
        var interaction = new FakeUserInteractionService { ConfirmSwitch = true };
        var logger = new FakeExecutionLogService();
        var orchestrator = new SwitchOrchestrator(
            state,
            runner,
            new FakeCheckEvaluator(),
            new FakeWallpaperService(),
            logger,
            interaction);

        var first = orchestrator.SwitchAsync("target");
        await runner.Started.Task;
        var second = await orchestrator.SwitchAsync("target");
        gate.SetResult(true);
        await first;

        Assert.Equal(SwitchExecutionStatus.Rejected, second.Status);
    }

    private static async Task<FakeState> CreateLoadedStateAsync()
    {
        var state = new FakeState(new AppSettings
        {
            LastActiveProfileId = "current",
            Profiles =
            [
                new ProfileDefinition
                {
                    Id = "current",
                    Name = "Current",
                    ShutdownActions = [new CommandExecutionActionDefinition { Name = "shutdown-1", FileName = "cmd.exe" }]
                },
                new ProfileDefinition
                {
                    Id = "target",
                    Name = "Target",
                    LaunchActions =
                    [
                        new CommandExecutionActionDefinition { Name = "launch-1", FileName = "cmd.exe" },
                        new CommandExecutionActionDefinition { Name = "launch-2", FileName = "cmd.exe" }
                    ]
                }
            ]
        });
        await state.LoadAsync();
        return state;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "workspice-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeState(AppSettings settings) : IWorkspiceState
    {
        public AppSettings Settings { get; private set; } = settings;
        public AppState State { get; private set; } = AppState.NoActiveProfile();
        public ProfileDefinition? CurrentProfile => FindProfile(State.GetStableProfileId());

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            State = AppState.ActiveProfile(Settings.LastActiveProfileId!);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ProfileDefinition? FindProfile(string? profileId)
            => Settings.Profiles.FirstOrDefault(profile => profile.Id == profileId);

        public void SetState(AppState state) => State = state;
        public void ReplaceSettings(AppSettings settings) => Settings = settings;
        public void SetLastActiveProfile(string? profileId) => Settings.LastActiveProfileId = profileId;
    }

    private sealed class FakeActionRunner(params ExternalCommandResult[] results) : IActionRunner
    {
        private readonly Queue<ExternalCommandResult> _results = new(results);
        public List<string> ExecutedActionNames { get; } = [];

        public Task<ExternalCommandResult> RunAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        {
            ExecutedActionNames.Add(action.Name);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class BlockingActionRunner(TaskCompletionSource<bool> gate) : IActionRunner
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ExternalCommandResult> RunAsync(ActionDefinition action, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await gate.Task.WaitAsync(cancellationToken);
            return new ExternalCommandResult { Succeeded = true };
        }
    }

    private sealed class FakeCheckEvaluator : ICheckEvaluator
    {
        public Task<CheckEvaluationResult> EvaluateAsync(CheckDefinition check, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckEvaluationResult { Passed = true });

        public Task<PostCheckEvaluationResult> EvaluatePostCheckAsync(PostCheckDefinition definition, CancellationToken cancellationToken = default)
            => Task.FromResult(new PostCheckEvaluationResult { Passed = true });
    }

    private sealed class FakeWallpaperService : IWallpaperService
    {
        public Task<string?> ApplyAsync(ProfileDefinition profile, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("wallpaper.png");
    }

    private sealed class FakeExecutionLogService : IExecutionLogService
    {
        public List<SwitchExecutionResult> Results { get; } = [];

        public Task WriteAsync(SwitchExecutionResult result, int retentionDays, CancellationToken cancellationToken = default)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserInteractionService : IUserInteractionService
    {
        public bool ConfirmSwitch { get; set; } = true;
        public FailureResolution FailureResolution { get; set; } = FailureResolution.Retry;

        public Task<bool> ConfirmSwitchAsync(ProfileDefinition? currentProfile, ProfileDefinition targetProfile, CancellationToken cancellationToken = default)
            => Task.FromResult(ConfirmSwitch);

        public Task<ActionPromptDecision> PromptActionAsync(ActionDefinition action, CancellationToken cancellationToken = default)
            => Task.FromResult(ActionPromptDecision.Run);

        public Task<FailureResolution> ResolveFailureAsync(ActionDefinition action, ActionExecutionResult executionResult, CancellationToken cancellationToken = default)
            => Task.FromResult(FailureResolution);

        public Task ShowErrorAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
