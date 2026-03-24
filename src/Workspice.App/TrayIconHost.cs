using System.Drawing;
using System.Windows.Forms;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;

namespace Workspice.App;

public sealed class TrayIconHost(
    IWorkspiceState state,
    ISwitchOrchestrator switchOrchestrator,
    IAutoStartService autoStartService) : IDisposable
{
    private NotifyIcon? _notifyIcon;

    public async Task InitializeAsync()
    {
        await state.LoadAsync();
        _notifyIcon = new NotifyIcon
        {
            Text = "Workspice",
            Visible = true,
            Icon = SystemIcons.Application
        };
        _notifyIcon.DoubleClick += async (_, _) => await OpenProfileEditorAsync();
        await RefreshMenuAsync();
    }

    public Task RefreshMenuAsync()
    {
        if (_notifyIcon is null)
        {
            return Task.CompletedTask;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(state.State.ToDisplayText()) { Enabled = false });
        if (state.State.Kind == AppStateKind.AttentionRequired)
        {
            menu.Items.Add(new ToolStripMenuItem("警告: 再試行または別プロファイルへ切替してください") { Enabled = false });
        }

        menu.Items.Add(new ToolStripSeparator());

        foreach (var profile in state.Settings.Profiles)
        {
            var item = new ToolStripMenuItem(profile.Name)
            {
                Checked = string.Equals(profile.Id, state.Settings.LastActiveProfileId, StringComparison.OrdinalIgnoreCase),
                Enabled = state.State.Kind != AppStateKind.Transitioning
            };
            item.Click += async (_, _) =>
            {
                await switchOrchestrator.SwitchAsync(profile.Id);
                await RefreshMenuAsync();
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var editProfiles = new ToolStripMenuItem("プロファイル編集");
        editProfiles.Click += async (_, _) => await OpenProfileEditorAsync();
        menu.Items.Add(editProfiles);

        var exit = new ToolStripMenuItem("終了");
        exit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = menu;
        return Task.CompletedTask;
    }

    private async Task OpenProfileEditorAsync()
    {
        var window = new Views.ProfileEditorWindow(ModelCloneHelper.Clone(state.Settings));
        if (window.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var updatedSettings = window.ResultSettings;
            await autoStartService.SetEnabledAsync(updatedSettings.StartWithWindows);
            state.ReplaceSettings(updatedSettings);
            await state.SaveAsync();
            await RefreshMenuAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Workspice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
}
