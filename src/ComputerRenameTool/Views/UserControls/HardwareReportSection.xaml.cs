using System.Windows.Controls;

namespace ComputerRenameTool.Views.UserControls;

/// <summary>
/// "Hardware report" section. Tab 2 of the main window shows a 5-card summary
/// row on top, with collapsible <c>Expander</c> panels for the per-category
/// detail below (FIX-REQUEST-8 — TabControl split, scrollable 500px window).
/// </summary>
public partial class HardwareReportSection : UserControl
{
    public HardwareReportSection()
    {
        InitializeComponent();
    }
}