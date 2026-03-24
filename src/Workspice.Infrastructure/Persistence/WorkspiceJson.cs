using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workspice.Infrastructure.Persistence;

public static class WorkspiceJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
