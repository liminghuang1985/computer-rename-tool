using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using ComputerRenameTool.ViewModels;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Regression tests for the post-rename UI refresh wiring. The reported bug
/// was that the "current computer name" label at the top of the main window
/// kept showing the old name after a successful rename, so these tests pin
/// the contract that <see cref="MainViewModel"/> re-reads the hardware report
/// once <see cref="RenameViewModel.IsSubmitSuccess"/> flips to <c>true</c>.
/// </summary>
public class MainViewModelTests
{
    private sealed class FakeSystemInfo : ISystemInfoService
    {
        public ComputerInfo Computer { get; set; } = new("OLD-NAME", "Windows 11", "user");
        public HardwareInfo Hardware { get; set; } = new("CPU", "16 GB", "GPU", "Disk");

        public ComputerInfo GetComputerInfo() => Computer;
        public HardwareInfo GetHardwareInfo() => Hardware;
        public CpuInfo? GetCpuInfo() => null;
        public OperatingSystemInfo? GetOperatingSystemInfo() => null;
        public BiosInfo? GetBiosInfo() => null;
        public MotherboardInfo? GetMotherboardInfo() => null;
        public IReadOnlyList<MemoryChip> GetMemoryChips() => Array.Empty<MemoryChip>();
        public int? GetMemorySlotCount() => null;
        public IReadOnlyList<PhysicalDisk> GetPhysicalDisks() => Array.Empty<PhysicalDisk>();
        public IReadOnlyList<LogicalDisk> GetLogicalDisks() => Array.Empty<LogicalDisk>();
        public IReadOnlyList<GpuInfo> GetGpus() => Array.Empty<GpuInfo>();
        public IReadOnlyList<NetworkAdapter> GetNetworkAdapters() => Array.Empty<NetworkAdapter>();
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

    private sealed class FakeHardwareReportService : IHardwareReportService
    {
        public HardwareReport? NextReport { get; set; }
        public int CallCount { get; private set; }

        public Task<HardwareReport> CollectAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(NextReport ?? HardwareReport.Empty(new ComputerInfo("FRESH", "Win", "user")));
        }
    }

    private static MainViewModel BuildViewModel(
        FakeSystemInfo info,
        out FakeHardwareReportService hw)
    {
        hw = new FakeHardwareReportService();
        return new MainViewModel(info, new FakeRenameService(), new FakeAdmin(), hw);
    }

    [Fact]
    public void ComputerName_InitiallyMirrorsSystemInfo()
    {
        var info = new FakeSystemInfo();
        var vm = BuildViewModel(info, out _);
        Assert.Equal("OLD-NAME", vm.Rename.CurrentName);
    }

    [Fact]
    public void Constructor_PopulatesRenameAndHardware()
    {
        var info = new FakeSystemInfo();
        var vm = BuildViewModel(info, out _);

        Assert.NotNull(vm.Rename);
        Assert.NotNull(vm.Hardware);
        // Hardware starts in loading state until the service returns.
        Assert.True(vm.Hardware.IsLoading);
    }

    [Fact]
    public async Task Hardware_RefreshesAfterCollect()
    {
        var info = new FakeSystemInfo();
        var hw = new FakeHardwareReportService
        {
            NextReport = new HardwareReport(
                info.GetComputerInfo(),
                new CpuInfo("Intel i7", 20, 28, 5.4, (ushort)23),
                new OperatingSystemInfo("Windows 11 Pro", "10.0", "22631", null, null, null, 0, 0),
                new BiosInfo("Dell", "1.5", "2024-01-15"),
                new MotherboardInfo("Dell Inc.", "Latitude 5540", "ABC123"),
                new[] { new MemoryChip("DIMM A1", "Samsung", "M...", 17179869184UL, 4800, 13, 1) },
                MemorySlotCount: 4,
                new[] { new PhysicalDisk("Samsung 990 PRO", 1024UL * 1024 * 1024 * 1024, "NVMe", "OK", "S123") },
                new[] { new LogicalDisk("C:", "System", 500UL * 1024 * 1024 * 1024, 250UL * 1024 * 1024 * 1024) },
                new[] { new GpuInfo("NVIDIA RTX 4060", "31.0.15.4601", 8UL * 1024 * 1024 * 1024, "AD107") },
                new[] { new NetworkAdapter("Wi-Fi", "WiFi", "aa:bb:cc:dd:ee:ff", 1_000_000_000UL, "10.12.138.38", "255.255.255.0", "10.12.138.1") }),
        };
        var vm = new MainViewModel(info, new FakeRenameService(), new FakeAdmin(), hw);

        // Wait for the background task to complete.
        WaitFor(() => !vm.Hardware.IsLoading);

        Assert.False(vm.Hardware.IsLoading);
        Assert.Equal("Intel i7", vm.Hardware.Cpu.DisplayName);
        Assert.Equal("Windows 11 Pro", vm.Hardware.OperatingSystem.DisplayName);
        Assert.Equal("Dell Inc.", vm.Hardware.Motherboard.Manufacturer);
        Assert.Single(vm.Hardware.MemoryChips);
        Assert.Equal("Samsung", vm.Hardware.MemoryChips[0].Manufacturer);
        Assert.Single(vm.Hardware.PhysicalDisks);
        Assert.Equal("NVMe", vm.Hardware.PhysicalDisks[0].Interface);
        Assert.Equal("WiFi", vm.Hardware.NetworkAdapters[0].Connection);
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
        var hw = new FakeHardwareReportService();
        var vm = new MainViewModel(info, rename, new FakeAdmin(), hw);

        // Simulate the kernel accepting the rename: the next WMI read returns
        // the new name.
        hw.NextReport = new HardwareReport(
            new ComputerInfo("NEW-NAME", "Windows 11", "user"),
            null, null, null, null,
            Array.Empty<MemoryChip>(),
            MemorySlotCount: null,
            Array.Empty<PhysicalDisk>(),
            Array.Empty<LogicalDisk>(),
            Array.Empty<GpuInfo>(),
            Array.Empty<NetworkAdapter>());

        // Drive the rename through the VM so PropertyChanged fires.
        vm.Rename.InputName = "NEW-NAME";
        vm.Rename.SubmitCommand.Execute(null);

        // Wait for the async submit + the post-rename refresh.
        WaitFor(() => hw.CallCount >= 2);

        Assert.Equal("NEW-NAME", vm.Rename.CurrentName);
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
        var hw = new FakeHardwareReportService();
        var vm = new MainViewModel(info, rename, new FakeAdmin(), hw);

        // Capture the call count after the initial load.
        WaitFor(() => !vm.Hardware.IsLoading);
        var callsAfterLoad = hw.CallCount;

        vm.Rename.InputName = "NEW-NAME";
        vm.Rename.SubmitCommand.Execute(null);
        WaitFor(() => !vm.Rename.IsSubmitting);

        Assert.False(vm.Rename.IsSubmitSuccess);
        Assert.Equal("OLD-NAME", vm.Rename.CurrentName);
        Assert.Equal(callsAfterLoad, hw.CallCount);
    }

    [Fact]
    public void RenameCompleted_FiresOnSuccess()
    {
        var info = new FakeSystemInfo();
        var rename = new FakeRenameService
        {
            Result = RenameResult.Success("NEW-NAME"),
        };
        var vm = new MainViewModel(info, rename, new FakeAdmin(), new FakeHardwareReportService());

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
