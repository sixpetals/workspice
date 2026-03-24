using System.Text.Json;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.Infrastructure.Persistence;

public sealed class JsonProfileRepository(WorkspicePathOptions pathOptions) : IProfileRepository
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pathOptions.SettingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(pathOptions.SettingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, WorkspiceJson.SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("設定ファイルの読み込み結果が null でした。");

        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"未対応の SchemaVersion です: {settings.SchemaVersion}");
        }

        settings.Profiles ??= [];
        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        Directory.CreateDirectory(Path.GetDirectoryName(pathOptions.SettingsPath)!);

        var tempPath = $"{pathOptions.SettingsPath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, WorkspiceJson.SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, pathOptions.SettingsPath, true);
    }
}
