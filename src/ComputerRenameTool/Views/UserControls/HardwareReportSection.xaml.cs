using System.Windows;
using System.Windows.Controls;
using ComputerRenameTool.ViewModels;

namespace ComputerRenameTool.Views.UserControls;

/// <summary>
/// "Hardware report" section. Replaces the older <see cref="HardwareInfoSection"/>
/// with a summary + collapsible detail view (FIX-REQUEST-7 §UI 重新设计).
/// </summary>
public partial class HardwareReportSection : UserControl
{
    public HardwareReportSection()
    {
        InitializeComponent();
    }

    private void ExpandToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HardwareReportViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
        }
    }
}
