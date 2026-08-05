using System.ComponentModel;
using System.Windows;
using ComputerRenameTool.Helpers;
using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using ComputerRenameTool.ViewModels;

namespace ComputerRenameTool.Views;

/// <summary>
/// Main window code-behind. The view-model is constructed once via the XAML
/// <c>Window.Resources</c> declaration. This class only handles window-level
/// events: pending-reboot toast, post-rename reboot prompt, and the reboot
/// countdown (DESIGN.md §6 startup + §6.2 rename flows).
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly IRebootService _reboot;
    private RebootPromptWindow? _rebootPrompt;

    public MainWindow() : this(new RebootService())
    {
    }

    public MainWindow(IRebootService reboot)
    {
        InitializeComponent();
        _vm = (MainViewModel)Resources["MainVM"];
        _reboot = reboot;

        _vm.Rename.PropertyChanged += OnRenameViewModelPropertyChanged;
        _reboot.CountdownTick += OnRebootCountdownTick;
        Closing += OnClosing;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        ShowPendingRebootToast();
    }

    /// <summary>
    /// If a previous session asked for "稍后重启", surface that reminder now.
    /// </summary>
    private void ShowPendingRebootToast()
    {
        var marker = ToastNotifier.ReadPending();
        if (marker is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"上次修改了机器名 ({marker.OldName} → {marker.NewName}),但尚未重启。\n\n是否立即重启?",
            "重启提醒",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        ToastNotifier.Clear();

        if (answer == MessageBoxResult.Yes)
        {
            StartRebootCountdown();
        }
    }

    private void OnRenameViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RenameViewModel.IsSubmitSuccess)) return;
        if (!_vm.Rename.IsSubmitSuccess) return;

        Dispatcher.BeginInvoke(new Action(OnRenameSucceeded));
    }

    private void OnRenameSucceeded()
    {
        var result = MessageBox.Show(
            this,
            "机器名修改成功!\n\n需要重启电脑后生效。\n\n点【是】立即重启(60 秒倒计时)。\n点【否】稍后重启(下次开机时提醒)。",
            "修改成功",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            StartRebootCountdown();
        }
        else
        {
            ToastNotifier.MarkPending(_vm.Rename.CurrentName, _vm.Rename.InputName);
            Application.Current.Shutdown();
        }
    }

    private void StartRebootCountdown()
    {
        try
        {
            _rebootPrompt = new RebootPromptWindow { Owner = this };
            _rebootPrompt.Show();
            _reboot.InitiateReboot(60);
        }
        catch (Exception ex)
        {
            App.Logger?.Error("Failed to initiate reboot.", ex);
            MessageBox.Show(
                this,
                RenameResult.MapHResultToMessage(0x80070015),
                "启动重启失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnRebootCountdownTick(object? sender, RebootCountdownEventArgs e)
    {
        if (_rebootPrompt is null) return;
        _rebootPrompt.Dispatcher.BeginInvoke(new Action(() =>
            _rebootPrompt.UpdateCountdown(e.SecondsRemaining)));
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_rebootPrompt is null) return;
        try { _reboot.CancelReboot(); } catch { }
        _rebootPrompt.Close();
        _rebootPrompt = null;
    }
}
