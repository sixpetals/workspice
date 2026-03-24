using System.Windows;
using Workspice.Domain.Models;

namespace Workspice.App.Views;

public partial class CheckEditorWindow : Window
{
    public CheckDefinition ResultCheck { get; private set; }

    public CheckEditorWindow(CheckDefinition source)
    {
        InitializeComponent();
        ResultCheck = ModelCloneHelper.Clone(source);
        CommandTextBox.Text = ResultCheck.Command;
        ArgumentsTextBox.Text = ResultCheck.Arguments;
        ExitCodesTextBox.Text = string.Join(",", ResultCheck.SuccessExitCodes);
        OutputRegexTextBox.Text = ResultCheck.OutputRegex ?? string.Empty;
        NegateCheckBox.IsChecked = ResultCheck.Negate;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommandTextBox.Text))
        {
            System.Windows.MessageBox.Show("Command は必須です。");
            return;
        }

        var exitCodes = ExitCodesTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, out var parsed) ? parsed : (int?)null)
            .ToList();

        if (exitCodes.Any(static value => value is null))
        {
            System.Windows.MessageBox.Show("SuccessExitCodes はカンマ区切りの整数で指定してください。");
            return;
        }

        ResultCheck.Command = CommandTextBox.Text;
        ResultCheck.Arguments = ArgumentsTextBox.Text;
        ResultCheck.SuccessExitCodes = exitCodes.Select(static value => value!.Value).ToList();
        ResultCheck.OutputRegex = string.IsNullOrWhiteSpace(OutputRegexTextBox.Text) ? null : OutputRegexTextBox.Text;
        ResultCheck.Negate = NegateCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
