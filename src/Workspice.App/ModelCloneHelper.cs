using System.Text.Json;
using Workspice.Domain.Models;
using Workspice.Infrastructure.Persistence;

namespace Workspice.App;

public static class ModelCloneHelper
{
    public static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, WorkspiceJson.SerializerOptions);
        return JsonSerializer.Deserialize<T>(json, WorkspiceJson.SerializerOptions)
            ?? throw new InvalidOperationException("モデルの複製に失敗しました。");
    }

    public static ActionDefinition CloneAction(ActionDefinition action)
    {
        var json = JsonSerializer.Serialize(action, action.GetType(), WorkspiceJson.SerializerOptions);
        return (ActionDefinition)(JsonSerializer.Deserialize(json, action.GetType(), WorkspiceJson.SerializerOptions)
            ?? throw new InvalidOperationException("アクション複製に失敗しました。"));
    }
}
