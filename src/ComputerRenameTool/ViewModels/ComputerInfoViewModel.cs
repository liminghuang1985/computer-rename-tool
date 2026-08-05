using ComputerRenameTool.MVVM;
using ComputerRenameTool.Models;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// View-model for the "current computer info" section. Exposes
/// <see cref="ComputerName"/>, <see cref="WindowsVersion"/> and
/// <see cref="CurrentUser"/> directly for XAML binding.
/// </summary>
public sealed class ComputerInfoViewModel : ObservableObject
{
    public ComputerInfoViewModel(ComputerInfo info)
    {
        ComputerName = info.ComputerName;
        WindowsVersion = info.WindowsVersion;
        CurrentUser = info.CurrentUser;
    }

    public string ComputerName { get; }
    public string WindowsVersion { get; }
    public string CurrentUser { get; }
}
