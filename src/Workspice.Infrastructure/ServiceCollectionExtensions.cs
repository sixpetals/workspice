using Microsoft.Extensions.DependencyInjection;
using Workspice.Application.Contracts;
using Workspice.Infrastructure.Execution;
using Workspice.Infrastructure.Logging;
using Workspice.Infrastructure.Persistence;
using Workspice.Infrastructure.Startup;
using Workspice.Infrastructure.Wallpaper;

namespace Workspice.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspiceInfrastructure(this IServiceCollection services, string executablePath)
    {
        var paths = WorkspicePathOptions.CreateDefault();
        services.AddSingleton(paths);
        services.AddSingleton<ProcessInvoker>();
        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<IActionRunner, DefaultActionRunner>();
        services.AddSingleton<ICheckEvaluator, DefaultCheckEvaluator>();
        services.AddSingleton<IExecutionLogService, FileExecutionLogService>();
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<IAutoStartService>(_ => new TaskSchedulerAutoStartService(executablePath));
        return services;
    }
}
