using System.Windows;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.App;

public sealed class WpfDialogService : IUserInteractionService
{
    public Task<bool> ConfirmSwitchAsync(ProfileDefinition? currentProfile, ProfileDefinition targetProfile, CancellationToken cancellationToken = default)
    {
        var message =
            $"切替元: {currentProfile?.Name ?? "(なし)"}{Environment.NewLine}" +
            $"切替先: {targetProfile.Name}{Environment.NewLine}" +
            $"終了時アクション: {currentProfile?.ShutdownActions.Count ?? 0} 件{Environment.NewLine}" +
            $"起動時アクション: {targetProfile.LaunchActions.Count} 件";

        var result = System.Windows.MessageBox.Show(message, "プロファイル切替確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.OK);
    }

    public Task<ActionPromptDecision> PromptActionAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        var message =
            $"アクション名: {action.Name}{Environment.NewLine}" +
            $"種別: {action.ActionType}{Environment.NewLine}" +
            $"対象: {action.DescribeTarget()}{Environment.NewLine}" +
            $"引数: {action.DescribeArguments()}{Environment.NewLine}{Environment.NewLine}" +
            "はい = 実行 / いいえ = スキップ / キャンセル = 中止";

        var result = System.Windows.MessageBox.Show(message, "アクション確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return Task.FromResult(result switch
        {
            MessageBoxResult.Yes => ActionPromptDecision.Run,
            MessageBoxResult.No => ActionPromptDecision.Skip,
            _ => ActionPromptDecision.Abort
        });
    }

    public Task<FailureResolution> ResolveFailureAsync(ActionDefinition action, ActionExecutionResult executionResult, CancellationToken cancellationToken = default)
    {
        var message =
            $"アクション: {action.Name}{Environment.NewLine}" +
            $"状態: {executionResult.Status}{Environment.NewLine}" +
            $"理由: {executionResult.FailureReason ?? "(詳細なし)"}{Environment.NewLine}{Environment.NewLine}" +
            "はい = 再試行 / いいえ = 残りを続行 / キャンセル = 中止";
        var result = System.Windows.MessageBox.Show(message, "アクション失敗", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return Task.FromResult(result switch
        {
            MessageBoxResult.Yes => FailureResolution.Retry,
            MessageBoxResult.No => FailureResolution.Continue,
            _ => FailureResolution.Abort
        });
    }

    public Task ShowErrorAsync(string message, CancellationToken cancellationToken = default)
    {
        System.Windows.MessageBox.Show(message, "Workspice", MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }
}
