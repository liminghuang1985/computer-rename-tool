using System.Windows;
using System.Windows.Controls;
using ComputerRenameTool.ViewModels;

namespace ComputerRenameTool.Views.UserControls;

/// <summary>
/// "Rename" section. Code-behind is intentionally minimal — all behaviour
/// lives in <see cref="RenameViewModel"/>. The only piece left here is the
/// pre-rename confirmation dialog (DESIGN.md §6.2 step 3), which is a pure
/// view concern and so lives in code-behind rather than the VM.
/// </summary>
public partial class RenameSection : UserControl
{
    public RenameSection() => InitializeComponent();

    private void RenameButton_Click(object? sender, RoutedEventArgs e)
    {
        // Commit pending text-box binding before reading view-model state.
        if (RenameInput is not null)
        {
            var binding = RenameInput.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }

        if (DataContext is not RenameViewModel vm) return;
        if (!vm.CanSubmit) return;

        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(
            owner,
            $"是否确认修改机器名?\n\n当前:{vm.CurrentName}\n新的:{vm.InputName}",
            "确认修改",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.OK && vm.SubmitCommand.CanExecute(null))
        {
            vm.SubmitCommand.Execute(null);
        }
    }
}
