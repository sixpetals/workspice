using System.Collections.ObjectModel;
using System.Windows;
using Workspice.Domain.Models;

namespace Workspice.App.Views;

public partial class ProfileEditorWindow : Window
{
    private ProfileDefinition? _selectedProfile;

    public ObservableCollection<ProfileDefinition> Profiles { get; }
    public AppSettings ResultSettings { get; private set; }

    public ProfileEditorWindow(AppSettings settings)
    {
        InitializeComponent();
        ResultSettings = ModelCloneHelper.Clone(settings);
        Profiles = new ObservableCollection<ProfileDefinition>(ResultSettings.Profiles.Select(ModelCloneHelper.Clone));
        ProfilesListBox.ItemsSource = Profiles;
        WallpaperModeComboBox.ItemsSource = Enum.GetValues<WallpaperMode>();
        StartWithWindowsCheckBox.IsChecked = ResultSettings.StartWithWindows;
        LogRetentionDaysTextBox.Text = ResultSettings.LogRetentionDays.ToString();

        if (Profiles.Count > 0)
        {
            ProfilesListBox.SelectedIndex = 0;
        }
        else
        {
            AddProfile();
        }
    }

    private void ProfilesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedProfile = ProfilesListBox.SelectedItem as ProfileDefinition;
        LoadProfileDetails();
    }

    private void LoadProfileDetails()
    {
        if (_selectedProfile is null)
        {
            ProfileNameTextBox.Text = string.Empty;
            ProfileDescriptionTextBox.Text = string.Empty;
            WallpaperPathTextBox.Text = string.Empty;
            LaunchActionsListBox.ItemsSource = null;
            ShutdownActionsListBox.ItemsSource = null;
            return;
        }

        ProfileNameTextBox.Text = _selectedProfile.Name;
        ProfileDescriptionTextBox.Text = _selectedProfile.Description;
        WallpaperModeComboBox.SelectedItem = _selectedProfile.WallpaperMode;
        WallpaperPathTextBox.Text = _selectedProfile.WallpaperPath ?? string.Empty;
        LaunchActionsListBox.ItemsSource = _selectedProfile.LaunchActions;
        ShutdownActionsListBox.ItemsSource = _selectedProfile.ShutdownActions;
    }

    private void ProfileFields_Changed(object? sender, EventArgs e)
    {
        if (_selectedProfile is null)
        {
            return;
        }

        _selectedProfile.Name = ProfileNameTextBox.Text;
        _selectedProfile.Description = ProfileDescriptionTextBox.Text;
        _selectedProfile.WallpaperMode = WallpaperModeComboBox.SelectedItem is WallpaperMode mode
            ? mode
            : WallpaperMode.GeneratedFromProfileName;
        _selectedProfile.WallpaperPath = string.IsNullOrWhiteSpace(WallpaperPathTextBox.Text) ? null : WallpaperPathTextBox.Text;
        ProfilesListBox.Items.Refresh();
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e) => AddProfile();

    private void AddProfile()
    {
        var profile = new ProfileDefinition { Name = $"新しいプロファイル {Profiles.Count + 1}" };
        Profiles.Add(profile);
        ProfilesListBox.SelectedItem = profile;
    }

    private void CloneProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is null)
        {
            return;
        }

        var clone = ModelCloneHelper.Clone(_selectedProfile);
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Name = $"{clone.Name} コピー";
        Profiles.Add(clone);
        ProfilesListBox.SelectedItem = clone;
    }

    private void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is null)
        {
            return;
        }

        Profiles.Remove(_selectedProfile);
        ProfilesListBox.SelectedItem = Profiles.FirstOrDefault();
    }

    private void AddLaunchAction_Click(object sender, RoutedEventArgs e) => AddAction(isLaunch: true);
    private void AddShutdownAction_Click(object sender, RoutedEventArgs e) => AddAction(isLaunch: false);
    private void EditLaunchAction_Click(object sender, RoutedEventArgs e) => EditAction(LaunchActionsListBox, isLaunch: true);
    private void EditShutdownAction_Click(object sender, RoutedEventArgs e) => EditAction(ShutdownActionsListBox, isLaunch: false);
    private void RemoveLaunchAction_Click(object sender, RoutedEventArgs e) => RemoveAction(LaunchActionsListBox, isLaunch: true);
    private void RemoveShutdownAction_Click(object sender, RoutedEventArgs e) => RemoveAction(ShutdownActionsListBox, isLaunch: false);
    private void MoveLaunchActionUp_Click(object sender, RoutedEventArgs e) => MoveAction(LaunchActionsListBox, isLaunch: true, direction: -1);
    private void MoveLaunchActionDown_Click(object sender, RoutedEventArgs e) => MoveAction(LaunchActionsListBox, isLaunch: true, direction: 1);
    private void MoveShutdownActionUp_Click(object sender, RoutedEventArgs e) => MoveAction(ShutdownActionsListBox, isLaunch: false, direction: -1);
    private void MoveShutdownActionDown_Click(object sender, RoutedEventArgs e) => MoveAction(ShutdownActionsListBox, isLaunch: false, direction: 1);

    private void AddAction(bool isLaunch)
    {
        if (_selectedProfile is null)
        {
            return;
        }

        var editor = new ActionEditorWindow(null);
        if (editor.ShowDialog() != true)
        {
            return;
        }

        GetActionList(isLaunch).Add(editor.ResultAction);
        RefreshActionLists();
    }

    private void EditAction(System.Windows.Controls.ListBox sourceListBox, bool isLaunch)
    {
        if (_selectedProfile is null || sourceListBox.SelectedItem is not ActionDefinition selectedAction)
        {
            return;
        }

        var editor = new ActionEditorWindow(selectedAction);
        if (editor.ShowDialog() != true)
        {
            return;
        }

        var list = GetActionList(isLaunch);
        var index = list.IndexOf(selectedAction);
        list[index] = editor.ResultAction;
        RefreshActionLists();
    }

    private void RemoveAction(System.Windows.Controls.ListBox sourceListBox, bool isLaunch)
    {
        if (_selectedProfile is null || sourceListBox.SelectedItem is not ActionDefinition selectedAction)
        {
            return;
        }

        GetActionList(isLaunch).Remove(selectedAction);
        RefreshActionLists();
    }

    private void MoveAction(System.Windows.Controls.ListBox sourceListBox, bool isLaunch, int direction)
    {
        if (_selectedProfile is null || sourceListBox.SelectedItem is not ActionDefinition selectedAction)
        {
            return;
        }

        var list = GetActionList(isLaunch);
        var oldIndex = list.IndexOf(selectedAction);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= list.Count)
        {
            return;
        }

        list.RemoveAt(oldIndex);
        list.Insert(newIndex, selectedAction);
        RefreshActionLists();
        sourceListBox.SelectedItem = selectedAction;
    }

    private List<ActionDefinition> GetActionList(bool isLaunch)
    {
        return isLaunch ? _selectedProfile!.LaunchActions : _selectedProfile!.ShutdownActions;
    }

    private void RefreshActionLists()
    {
        LaunchActionsListBox.Items.Refresh();
        ShutdownActionsListBox.Items.Refresh();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Profiles.Any(profile => string.IsNullOrWhiteSpace(profile.Name)))
        {
            MessageBox.Show("プロファイル名は必須です。", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(LogRetentionDaysTextBox.Text, out var retentionDays) || retentionDays <= 0)
        {
            MessageBox.Show("ログ保持日数は 1 以上の整数で指定してください。", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultSettings.Profiles = Profiles.ToList();
        ResultSettings.LogRetentionDays = retentionDays;
        ResultSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        if (!ResultSettings.Profiles.Any(profile => string.Equals(profile.Id, ResultSettings.LastActiveProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            ResultSettings.LastActiveProfileId = null;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
