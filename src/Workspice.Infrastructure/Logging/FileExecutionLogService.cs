using System.Text.Json;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;
using Workspice.Infrastructure.Persistence;

namespace Workspice.Infrastructure.Logging;

public sealed class FileExecutionLogService(WorkspicePathOptions pathOptions) : IExecutionLogService
{
    public async Task WriteAsync(SwitchExecutionResult result, int retentionDays, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(pathOptions.LogsDirectory);
        Cleanup(retentionDays);

        var fileName = $"{result.StartedAt:yyyyMMdd_HHmmss}_{result.TargetProfileId}.json";
        var fullPath = Path.Combine(pathOptions.LogsDirectory, fileName);
        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, result, WorkspiceJson.SerializerOptions, cancellationToken);
    }

    private void Cleanup(int retentionDays)
    {
        if (retentionDays <= 0 || !Directory.Exists(pathOptions.LogsDirectory))
        {
            return;
        }

        var threshold = DateTime.UtcNow.AddDays(-retentionDays);
        foreach (var file in Directory.GetFiles(pathOptions.LogsDirectory, "*.json"))
        {
            try
            {
                if (File.GetCreationTimeUtc(file) < threshold)
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }
}
