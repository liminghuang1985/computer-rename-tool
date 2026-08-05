using ComputerRenameTool.MVVM;
using ComputerRenameTool.Services;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// Top-level view-model composed of the three section view-models. Holds
/// cross-cutting state (admin status, status-bar message) and owns the
/// elevation command (DESIGN.md §6.1 startup flow).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IAdminPrivilegeService _admin;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// XAML-friendly default constructor. Builds the production service
    /// graph. Tests use the explicit constructor to inject fakes.
    /// </summary>
    public MainViewModel() : this(
        new SystemInfoService(),
        new ComputerRenameService(),
        new AdminPrivilegeService(),
        suggestedName: null)
    {
    }

    public MainViewModel(
        ISystemInfoService systemInfo,
        IComputerRenameService renameService,
        IAdminPrivilegeService admin,
        string? suggestedName = null)
    {
        _admin = admin;
        IsAdmin = admin.IsRunAsAdmin();

        var computer = systemInfo.GetComputerInfo();
        var hardware = systemInfo.GetHardwareInfo();

        Computer = new ComputerInfoViewModel(computer);
        Hardware = new HardwareInfoViewModel(hardware);
        Rename = new RenameViewModel(renameService, computer.ComputerName, suggestedName);

        RequestElevationCommand = new RelayCommand(RequestElevation, () => !IsAdmin);
    }

    public ComputerInfoViewModel Computer { get; }
    public HardwareInfoViewModel Hardware { get; }
    public RenameViewModel Rename { get; }

    public bool IsAdmin { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand RequestElevationCommand { get; }

    private void RequestElevation()
    {
        if (_admin.RestartAsAdmin())
        {
            StatusMessage = "已请求管理员权限,工具将在新进程中重新启动...";
            // The new elevated instance will start on its own; close the current one.
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            StatusMessage = "用户取消提权,工具以只读模式运行。";
        }
    }
}
