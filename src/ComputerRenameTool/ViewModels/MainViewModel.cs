using System.ComponentModel;
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
    private readonly ISystemInfoService _systemInfo;
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
        _systemInfo = systemInfo;
        _admin = admin;
        IsAdmin = admin.IsRunAsAdmin();

        var computer = systemInfo.GetComputerInfo();
        var hardware = systemInfo.GetHardwareInfo();

        Computer = new ComputerInfoViewModel(computer);
        Hardware = new HardwareInfoViewModel(hardware);
        Rename = new RenameViewModel(renameService, computer.ComputerName, suggestedName);

        // After a successful rename, the registry has already been updated
        // even though the new name only takes effect on next boot. Refresh
        // the displayed name immediately so the user sees the change in the
        // UI without having to restart.
        Rename.PropertyChanged += OnRenamePropertyChanged;

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

    private void OnRenamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RenameViewModel.IsSubmitSuccess)) return;
        if (!Rename.IsSubmitSuccess) return;

        // Re-read system info so the top-of-window "current computer name"
        // reflects the freshly-applied name. The actual binding only uses
        // ComputerName, but reading the full ComputerInfo keeps the rest of
        // the section in sync in case other fields ever change.
        var info = _systemInfo.GetComputerInfo();
        Computer.ComputerName = info.ComputerName;
    }

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
