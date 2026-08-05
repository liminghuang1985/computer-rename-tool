using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using ComputerRenameTool.ViewModels;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Regression tests for the post-rename UI refresh wiring. The reported bug
/// was that the "current computer name" label at the top of the main window
/// kept showing the old name after a successful rename, so these tests pin
/// the contract that <see cref="MainViewModel"/> re-reads system info once
/// <see cref="RenameViewModel.IsSubmitSuccess"/> flips to <c>true</c>.
/// </summary>
public class MainViewModelTests
{
    private sealed class FakeSystemInfo : ISystemInfoService
    {
        public ComputerInfo Computer { get; set; } =
            new("OLD-NAME", "Windows 11", "user");
        public HardwareInfo Hardware { get; set; } =
            new("CPU", "16 GB", "GPU", "Disk");

        public ComputerInfo GetComputerInfo() => Computer;
        public HardwareInfo GetHardwareInfo() => Hardware;
    }

    private sealed class FakeAdmin : IAdminPrivilegeService
    {
        public bool IsRunAsAdmin() => true;
        public bool RestartAsAdmin() => false;
    }

    private sealed class FakeRenameService : IComputerRenameService
    {
        public RenameResult Result { get; set; } = RenameResult.Success("NEW-NAME");
        public RenameResult Rename(string newName) => Result;
    }

    [Fact]
    public void ComputerName_InitiallyMirrorsSystemInfo()
    {
        var info = new FakeSystemInfo();
        var vm = new MainViewModel(info, new FakeRenameService(), new FakeAdmin());
        Assert.Equal("OLD-NAME", vm.Computer.ComputerName);
    }

    [Fact]
    public void ComputerName_RefreshesAfterSuccessfulRename()
    {
        var info = new FakeSystemInfo
        {
            Computer = new ComputerInfo("OLD-NAME", "Windows 11", "user"),
        };
        var rename = new FakeRenameService
        {
            Result = RenameResult.Success("NEW-NAME"),
        };
        var vm = new MainViewModel(info, rename, new FakeAdmin());

        // Simulate the kernel accepting the rename: subsequent reads return
        // the new name.
        info.Computer = new ComputerInfo("NEW-NAME", "Windows 11", "user");

        // Drive the rename through the VM so PropertyChanged fires.
        vm.Rename.InputName = "NEW-NAME";
        vm.Rename.SubmitCommand.Execute(null);

        // Wait for the async submit to complete.
        WaitFor(() => vm.Rename.IsSubmitSuccess);

        // The displayed name should now be the new one, without a reboot.
        Assert.Equal("NEW-NAME", vm.Computer.ComputerName);
    }

    [Fact]
    public void ComputerName_NotRefreshedWhenRenameFails()
    {
        var info = new FakeSystemInfo
        {
            Computer = new ComputerInfo("OLD-NAME", "Windows 11", "user"),
        };
        var rename = new FakeRenameService
        {
            Result = RenameResult.Failed(unchecked((int)0x80070005), "denied"),
        };
        var vm = new MainViewModel(info, rename, new FakeAdmin());

        // Even if the underlying name somehow changed, the VM must not
        // touch the displayed name on failure.
        info.Computer = new ComputerInfo("CHANGED-ELSEWHERE", "Windows 11", "user");

        vm.Rename.InputName = "NEW-NAME";
        vm.Rename.SubmitCommand.Execute(null);
        WaitFor(() => !vm.Rename.IsSubmitting);

        Assert.False(vm.Rename.IsSubmitSuccess);
        Assert.Equal("OLD-NAME", vm.Computer.ComputerName);
    }

    [Fact]
    public void RenameCompleted_FiresOnSuccess()
    {
        var info = new FakeSystemInfo
        {
            Computer = new ComputerInfo("OLD-NAME", "Windows 11", "user"),
        };
        var rename = new FakeRenameService
        {
            Result = RenameResult.Success("NEW-NAME"),
        };
        var vm = new MainViewModel(info, rename, new FakeAdmin());

        RenameCompletedEventArgs? captured = null;
        vm.Rename.RenameCompleted += (_, e) => captured = e;

        vm.Rename.InputName = "NEW-NAME";
        vm.Rename.SubmitCommand.Execute(null);
        WaitFor(() => captured is not null);

        Assert.NotNull(captured);
        Assert.True(captured!.Result.IsSuccess);
        Assert.Equal("NEW-NAME", captured.Result.NewName);
    }

    private static void WaitFor(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition not met within timeout.");
            }
            System.Threading.Thread.Sleep(10);
        }
    }
}
