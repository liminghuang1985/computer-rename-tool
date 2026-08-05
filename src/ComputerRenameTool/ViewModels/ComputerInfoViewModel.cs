using ComputerRenameTool.MVVM;
using ComputerRenameTool.Models;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// View-model for the "current computer info" section. Exposes
/// <see cref="ComputerName"/>, <see cref="WindowsVersion"/> and
/// <see cref="CurrentUser"/> directly for XAML binding. The fields are
/// mutable so the UI can refresh immediately after a successful rename
/// (without waiting for a reboot, the registry is already updated).
/// </summary>
public sealed class ComputerInfoViewModel : ObservableObject
{
    private string _computerName;
    private string _windowsVersion;
    private string _currentUser;

    public ComputerInfoViewModel(ComputerInfo info)
    {
        _computerName = info.ComputerName;
        _windowsVersion = info.WindowsVersion;
        _currentUser = info.CurrentUser;
    }

    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    public string WindowsVersion
    {
        get => _windowsVersion;
        set => SetProperty(ref _windowsVersion, value);
    }

    public string CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }
}
