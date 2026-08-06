using System.ComponentModel;
using ComputerRenameTool.Models;
using ComputerRenameTool.MVVM;
using ComputerRenameTool.Services;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// Top-level view-model composed of the rename section + hardware report. Holds
/// cross-cutting state (admin status, status-bar message) and owns the
/// elevation command. The hardware report is loaded asynchronously on a
/// background thread to keep WMI off the UI thread (FIX-REQUEST-7 §关键实现要求 2-3).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ISystemInfoService _systemInfo;
    private readonly IHardwareReportService _hardwareService;
    private readonly IAdminPrivilegeService _admin;
    private HardwareReportViewModel _hardware = null!;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// XAML-friendly default constructor. Builds the production service
    /// graph. Tests use the explicit constructor to inject fakes.
    /// </summary>
    public MainViewModel() : this(
        new SystemInfoService(),
        new ComputerRenameService(),
        new AdminPrivilegeService(),
        new HardwareReportService(),
        suggestedName: null)
    {
    }

    public MainViewModel(
        ISystemInfoService systemInfo,
        IComputerRenameService renameService,
        IAdminPrivilegeService admin,
        string? suggestedName = null)
        : this(systemInfo, renameService, admin, new HardwareReportService(systemInfo), suggestedName)
    {
    }

    public MainViewModel(
        ISystemInfoService systemInfo,
        IComputerRenameService renameService,
        IAdminPrivilegeService admin,
        IHardwareReportService hardwareService,
        string? suggestedName = null)
    {
        _systemInfo = systemInfo;
        _hardwareService = hardwareService;
        _admin = admin;
        IsAdmin = admin.IsRunAsAdmin();

        var computer = systemInfo.GetComputerInfo();
        Rename = new RenameViewModel(renameService, computer.ComputerName, suggestedName);

        // Hardware report is loaded asynchronously so the UI paints first.
        Hardware = new HardwareReportViewModel(HardwareReport.Empty(computer))
        {
            IsLoading = true,
        };
        _ = StartHardwareLoadAsync();

        // After a successful rename, the registry has already been updated
        // even though the new name only takes effect on next boot. Refresh
        // the displayed name immediately so the user sees the change in the
        // UI without having to restart.
        Rename.PropertyChanged += OnRenamePropertyChanged;

        RequestElevationCommand = new RelayCommand(RequestElevation, () => !IsAdmin);
    }

    public RenameViewModel Rename { get; }

    public HardwareReportViewModel Hardware
    {
        get => _hardware;
        private set => SetProperty(ref _hardware, value);
    }

    public bool IsAdmin { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand RequestElevationCommand { get; }

    private async Task StartHardwareLoadAsync()
    {
        try
        {
            var report = await _hardwareService.CollectAsync();
            Hardware = new HardwareReportViewModel(report) { IsLoading = false };
        }
        catch (Exception ex)
        {
            App.Logger?.Error("Hardware report load failed.", ex);
            Hardware.IsLoading = false;
        }
    }

    private async void OnRenamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RenameViewModel.IsSubmitSuccess)) return;
        if (!Rename.IsSubmitSuccess) return;

        // Re-read the full hardware report so the top-of-window "current
        // computer name" reflects the freshly-applied name (FIX-REQUEST-7
        // §关键实现要求 4).
        try
        {
            var report = await _hardwareService.CollectAsync();
            Rename.UpdateComputer(report.Computer);
            Hardware = new HardwareReportViewModel(report) { IsLoading = false };
        }
        catch (Exception ex)
        {
            App.Logger?.Error("Hardware report reload failed after rename.", ex);
        }
    }

    private void RequestElevation()
    {
        if (_admin.RestartAsAdmin())
        {
            StatusMessage = "已请求管理员权限,工具将在新进程中重新启动...";
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            StatusMessage = "用户取消提权,工具以只读模式运行。";
        }
    }
}
