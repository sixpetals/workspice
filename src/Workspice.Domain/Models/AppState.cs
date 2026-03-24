namespace Workspice.Domain.Models;

public sealed class AppState
{
    public AppStateKind Kind { get; init; }
    public string? ActiveProfileId { get; init; }
    public string? FromProfileId { get; init; }
    public string? ToProfileId { get; init; }
    public string? LastStableProfileId { get; init; }
    public string? FailedTargetProfileId { get; init; }

    public static AppState NoActiveProfile() => new() { Kind = AppStateKind.NoActiveProfile };

    public static AppState ActiveProfile(string profileId) => new()
    {
        Kind = AppStateKind.ActiveProfile,
        ActiveProfileId = profileId,
        LastStableProfileId = profileId
    };

    public static AppState Transitioning(string? fromProfileId, string toProfileId) => new()
    {
        Kind = AppStateKind.Transitioning,
        FromProfileId = fromProfileId,
        ToProfileId = toProfileId,
        LastStableProfileId = fromProfileId
    };

    public static AppState AttentionRequired(string? lastStableProfileId, string targetProfileId) => new()
    {
        Kind = AppStateKind.AttentionRequired,
        LastStableProfileId = lastStableProfileId,
        FailedTargetProfileId = targetProfileId
    };

    public string? GetStableProfileId()
    {
        return Kind switch
        {
            AppStateKind.ActiveProfile => ActiveProfileId,
            AppStateKind.AttentionRequired => LastStableProfileId,
            AppStateKind.Transitioning => LastStableProfileId,
            _ => null
        };
    }

    public string ToDisplayText()
    {
        return Kind switch
        {
            AppStateKind.NoActiveProfile => "アクティブプロファイルなし",
            AppStateKind.ActiveProfile => $"アクティブ: {ActiveProfileId}",
            AppStateKind.Transitioning => $"切替中: {FromProfileId ?? "(なし)"} -> {ToProfileId}",
            AppStateKind.AttentionRequired => $"要対応: {FailedTargetProfileId}",
            _ => Kind.ToString()
        };
    }
}
