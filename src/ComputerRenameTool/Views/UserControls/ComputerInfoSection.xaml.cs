using System.Windows;
using System.Windows.Controls;
using ComputerRenameTool.Helpers;
using ComputerRenameTool.ViewModels;

namespace ComputerRenameTool.Views.UserControls;

/// <summary>
/// "Current computer info" section. Code-behind is limited to the copy
/// button click handler — everything else is data-bound.
/// </summary>
public partial class ComputerInfoSection : UserControl
{
    public ComputerInfoSection()
    {
        InitializeComponent();
    }

    private void CopyNameButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComputerInfoViewModel vm)
        {
            ClipboardHelper.CopyText(vm.ComputerName);
        }
    }
}
