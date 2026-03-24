using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Workspice.Domain.Models;

namespace Workspice.App.Views;

public partial class ActionEditorWindow : Window
{
    private CheckDefinition _postCheckCommand = new();

    public ObservableCollection<CheckDefinition> Preconditions { get; }
    public ActionDefinition ResultAction { get; private set; }

    public ActionEditorWindow(ActionDefinition? action)
    {
        InitializeComponent();

        ResultAction = action is null
            ? new ApplicationLaunchActionDefinition { Name = "新しいアクション" }
            : ModelCloneHelper.CloneAction(action);

        Preconditions = new ObservableCollection<CheckDefinition>(ResultAction.Preconditions.Select(ModelCloneHelper.Clone));
        PreconditionsListBox.ItemsSource = Preconditions;
        ActionTypeComboBox.ItemsSource = Enum.GetValues<ActionType>();
        ApplicationRunLevelComboBox.ItemsSource = Enum.GetValues<RunLevel>();
        CommandRunLevelComboBox.ItemsSource = Enum.GetValues<RunLevel>();
        ServiceOperationComboBox.ItemsSource = Enum.GetValues<WindowsServiceOperation>();
        PostCheckModeComboBox.ItemsSource = new[] { "ProcessExists", "CommandCheck", "ServiceState" };
        PostCheckServiceStateComboBox.ItemsSource = Enum.GetValues<ServiceStateExpectation>();

        LoadAction();
    }

    private void LoadAction()
    {
        ActionTypeComboBox.SelectedItem = ResultAction.ActionType;
        ActionNameTextBox.Text = ResultAction.Name;
        TimeoutTextBox.Text = ResultAction.TimeoutSec.ToString();
        PromptBeforeRunCheckBox.IsChecked = ResultAction.PromptBeforeRun;
        EnabledCheckBox.IsChecked = ResultAction.Enabled;

        switch (ResultAction)
        {
            case ApplicationLaunchActionDefinition app:
                ExecutablePathTextBox.Text = app.ExecutablePath;
                ApplicationArgumentsTextBox.Text = app.Arguments;
                ApplicationWorkingDirectoryTextBox.Text = app.WorkingDirectory;
                ApplicationRunLevelComboBox.SelectedItem = app.RunLevel;
                break;
            case CommandExecutionActionDefinition command:
                CommandFileNameTextBox.Text = command.FileName;
                CommandArgumentsTextBox.Text = command.Arguments;
                CommandWorkingDirectoryTextBox.Text = command.WorkingDirectory;
                CommandRunLevelComboBox.SelectedItem = command.RunLevel;
                break;
            case WindowsServiceControlActionDefinition service:
                ServiceNameTextBox.Text = service.ServiceName;
                ServiceOperationComboBox.SelectedItem = service.Operation;
                break;
        }

        if (ResultAction.PostCheck is not null)
        {
            EnablePostCheckCheckBox.IsChecked = true;
            PostCheckTimeoutTextBox.Text = ResultAction.PostCheck.TimeoutSec.ToString();
            PostCheckPollTextBox.Text = ResultAction.PostCheck.PollIntervalMs.ToString();
            switch (ResultAction.PostCheck)
            {
                case ProcessExistsPostCheckDefinition process:
                    PostCheckModeComboBox.SelectedItem = "ProcessExists";
                    PostCheckProcessNameTextBox.Text = process.ProcessName;
                    break;
                case CommandCheckPostCheckDefinition commandCheck:
                    PostCheckModeComboBox.SelectedItem = "CommandCheck";
                    _postCheckCommand = ModelCloneHelper.Clone(commandCheck.Check);
                    UpdatePostCheckCommandSummary();
                    break;
                case ServiceStatePostCheckDefinition serviceState:
                    PostCheckModeComboBox.SelectedItem = "ServiceState";
                    PostCheckServiceNameTextBox.Text = serviceState.ServiceName;
                    PostCheckServiceStateComboBox.SelectedItem = serviceState.ExpectedState;
                    break;
            }
        }
        else
        {
            EnablePostCheckCheckBox.IsChecked = false;
            PostCheckTimeoutTextBox.Text = "30";
            PostCheckPollTextBox.Text = "1000";
            PostCheckModeComboBox.SelectedIndex = 0;
        }

        ApplyActionTypeVisibility();
        ApplyPostCheckVisibility();
        UpdatePostCheckCommandSummary();
    }

    private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyActionTypeVisibility();

    private void PostCheckModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyPostCheckVisibility();

    private void EnablePostCheckChanged(object sender, RoutedEventArgs e)
    {
        var enabled = EnablePostCheckCheckBox.IsChecked == true;
        PostCheckModeComboBox.IsEnabled = enabled;
        PostCheckTimeoutTextBox.IsEnabled = enabled;
        PostCheckPollTextBox.IsEnabled = enabled;
        ApplyPostCheckVisibility();
    }

    private void ApplyActionTypeVisibility()
    {
        var type = ActionTypeComboBox.SelectedItem is ActionType actionType ? actionType : ActionType.ApplicationLaunch;
        ApplicationLaunchPanel.Visibility = type == ActionType.ApplicationLaunch ? Visibility.Visible : Visibility.Collapsed;
        CommandExecutionPanel.Visibility = type == ActionType.CommandExecution ? Visibility.Visible : Visibility.Collapsed;
        ServiceControlPanel.Visibility = type == ActionType.WindowsServiceControl ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyPostCheckVisibility()
    {
        var enabled = EnablePostCheckCheckBox.IsChecked == true;
        var mode = PostCheckModeComboBox.SelectedItem as string;
        ProcessExistsPanel.Visibility = enabled && mode == "ProcessExists" ? Visibility.Visible : Visibility.Collapsed;
        CommandCheckPanel.Visibility = enabled && mode == "CommandCheck" ? Visibility.Visible : Visibility.Collapsed;
        ServiceStatePanel.Visibility = enabled && mode == "ServiceState" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddPrecondition_Click(object sender, RoutedEventArgs e)
    {
        var editor = new CheckEditorWindow(new CheckDefinition());
        if (editor.ShowDialog() == true)
        {
            Preconditions.Add(editor.ResultCheck);
        }
    }

    private void EditPrecondition_Click(object sender, RoutedEventArgs e)
    {
        if (PreconditionsListBox.SelectedItem is not CheckDefinition selected)
        {
            return;
        }

        var editor = new CheckEditorWindow(selected);
        if (editor.ShowDialog() != true)
        {
            return;
        }

        var index = Preconditions.IndexOf(selected);
        Preconditions[index] = editor.ResultCheck;
        PreconditionsListBox.Items.Refresh();
    }

    private void RemovePrecondition_Click(object sender, RoutedEventArgs e)
    {
        if (PreconditionsListBox.SelectedItem is CheckDefinition selected)
        {
            Preconditions.Remove(selected);
        }
    }

    private void EditPostCheckCommand_Click(object sender, RoutedEventArgs e)
    {
        var editor = new CheckEditorWindow(_postCheckCommand);
        if (editor.ShowDialog() == true)
        {
            _postCheckCommand = editor.ResultCheck;
            UpdatePostCheckCommandSummary();
        }
    }

    private void UpdatePostCheckCommandSummary()
    {
        PostCheckCommandSummaryTextBlock.Text = string.IsNullOrWhiteSpace(_postCheckCommand.Command)
            ? "未設定"
            : $"{_postCheckCommand.Command} {_postCheckCommand.Arguments}".Trim();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ActionNameTextBox.Text))
        {
            MessageBox.Show("アクション名は必須です。");
            return;
        }

        if (!int.TryParse(TimeoutTextBox.Text, out var timeoutSec) || timeoutSec <= 0)
        {
            MessageBox.Show("タイムアウトは 1 以上の整数で指定してください。");
            return;
        }

        ActionDefinition action = BuildAction(timeoutSec);
        action.Id = ResultAction.Id;
        action.Name = ActionNameTextBox.Text;
        action.TimeoutSec = timeoutSec;
        action.PromptBeforeRun = PromptBeforeRunCheckBox.IsChecked == true;
        action.Enabled = EnabledCheckBox.IsChecked == true;
        action.Preconditions = Preconditions.Select(ModelCloneHelper.Clone).ToList();
        try
        {
            action.PostCheck = BuildPostCheck();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        ResultAction = action;
        DialogResult = true;
    }

    private ActionDefinition BuildAction(int timeoutSec)
    {
        var type = ActionTypeComboBox.SelectedItem is ActionType actionType ? actionType : ActionType.ApplicationLaunch;
        return type switch
        {
            ActionType.ApplicationLaunch => new ApplicationLaunchActionDefinition
            {
                TimeoutSec = timeoutSec,
                ExecutablePath = ExecutablePathTextBox.Text,
                Arguments = ApplicationArgumentsTextBox.Text,
                WorkingDirectory = ApplicationWorkingDirectoryTextBox.Text,
                RunLevel = ApplicationRunLevelComboBox.SelectedItem is RunLevel appRunLevel ? appRunLevel : RunLevel.Elevated
            },
            ActionType.CommandExecution => new CommandExecutionActionDefinition
            {
                TimeoutSec = timeoutSec,
                FileName = CommandFileNameTextBox.Text,
                Arguments = CommandArgumentsTextBox.Text,
                WorkingDirectory = CommandWorkingDirectoryTextBox.Text,
                RunLevel = CommandRunLevelComboBox.SelectedItem is RunLevel cmdRunLevel ? cmdRunLevel : RunLevel.Elevated
            },
            ActionType.WindowsServiceControl => new WindowsServiceControlActionDefinition
            {
                TimeoutSec = timeoutSec,
                ServiceName = ServiceNameTextBox.Text,
                Operation = ServiceOperationComboBox.SelectedItem is WindowsServiceOperation operation ? operation : WindowsServiceOperation.Start
            },
            _ => throw new InvalidOperationException()
        };
    }

    private PostCheckDefinition? BuildPostCheck()
    {
        if (EnablePostCheckCheckBox.IsChecked != true)
        {
            return null;
        }

        if (!int.TryParse(PostCheckTimeoutTextBox.Text, out var timeoutSec) || timeoutSec <= 0)
        {
            MessageBox.Show("事後確認タイムアウトは 1 以上で指定してください。");
            throw new InvalidOperationException("PostCheck timeout is invalid.");
        }

        if (!int.TryParse(PostCheckPollTextBox.Text, out var pollMs) || pollMs <= 0)
        {
            MessageBox.Show("ポーリング間隔は 1 以上で指定してください。");
            throw new InvalidOperationException("PostCheck poll interval is invalid.");
        }

        var mode = PostCheckModeComboBox.SelectedItem as string;
        return mode switch
        {
            "ProcessExists" => new ProcessExistsPostCheckDefinition
            {
                TimeoutSec = timeoutSec,
                PollIntervalMs = pollMs,
                ProcessName = PostCheckProcessNameTextBox.Text
            },
            "CommandCheck" => new CommandCheckPostCheckDefinition
            {
                TimeoutSec = timeoutSec,
                PollIntervalMs = pollMs,
                Check = ModelCloneHelper.Clone(_postCheckCommand)
            },
            "ServiceState" => new ServiceStatePostCheckDefinition
            {
                TimeoutSec = timeoutSec,
                PollIntervalMs = pollMs,
                ServiceName = PostCheckServiceNameTextBox.Text,
                ExpectedState = PostCheckServiceStateComboBox.SelectedItem is ServiceStateExpectation expectation
                    ? expectation
                    : ServiceStateExpectation.Running
            },
            _ => null
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
