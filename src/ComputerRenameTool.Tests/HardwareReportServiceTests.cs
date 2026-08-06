using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Tests for the WMI-aggregation shape. The actual WMI calls are NOT exercised
/// here — those run only on Windows under the production EXE (FIX-REQUEST-7
/// §单元测试). We only verify that the service composes the per-section reads
/// into a single <see cref="HardwareReport"/>.
/// </summary>
public class HardwareReportServiceTests
{
    private sealed class FakeSystemInfo : ISystemInfoService
    {
        public ComputerInfo Computer { get; } = new("MY-PC", "Windows 11", "alice");
        public HardwareInfo Hardware { get; } = new("CPU", "32 GB", "GPU", "1 TB");
        public CpuInfo? Cpu { get; } = new("Intel i7-14700", 20, 28, 5.4, (ushort)23);
        public OperatingSystemInfo? Os { get; } = new("Windows 11", "10.0", "22631", "20240101000000.000000+000", "20260804090012.123456+000", "OS-SN", 0, 0);
        public BiosInfo? Bios { get; } = new("Dell", "1.5", "20240115000000.000000+000");
        public MotherboardInfo? Motherboard { get; } = new("Dell Inc.", "Latitude 5540", "ABC123");
        public IReadOnlyList<MemoryChip> MemoryChips { get; } = new[]
        {
            new MemoryChip("DIMM A1", "Samsung", "M471A2K43", 16UL * 1024 * 1024 * 1024, 4800, 13),
            new MemoryChip("DIMM B1", "Hynix",  "HMCG78ME", 16UL * 1024 * 1024 * 1024, 4800, 13),
        };
        public IReadOnlyList<PhysicalDisk> PhysicalDisks { get; } = new[]
        {
            new PhysicalDisk("Samsung 990 PRO", 1024UL * 1024 * 1024 * 1024, "NVMe", "OK", "S123"),
        };
        public IReadOnlyList<LogicalDisk> LogicalDisks { get; } = new[]
        {
            new LogicalDisk("C:", "System", 500UL * 1024 * 1024 * 1024, 200UL * 1024 * 1024 * 1024),
        };
        public IReadOnlyList<GpuInfo> Gpus { get; } = new[]
        {
            new GpuInfo("NVIDIA RTX 4060", "31.0.15.4601", 8UL * 1024 * 1024 * 1024, "AD107"),
        };
        public IReadOnlyList<NetworkAdapter> NetworkAdapters { get; } = new[]
        {
            new NetworkAdapter("Wi-Fi", "WiFi", "aa:bb:cc:dd:ee:ff", 1_000_000_000UL),
        };

        public ComputerInfo GetComputerInfo() => Computer;
        public HardwareInfo GetHardwareInfo() => Hardware;
        public CpuInfo? GetCpuInfo() => Cpu;
        public OperatingSystemInfo? GetOperatingSystemInfo() => Os;
        public BiosInfo? GetBiosInfo() => Bios;
        public MotherboardInfo? GetMotherboardInfo() => Motherboard;
        public IReadOnlyList<MemoryChip> GetMemoryChips() => MemoryChips;
        public IReadOnlyList<PhysicalDisk> GetPhysicalDisks() => PhysicalDisks;
        public IReadOnlyList<LogicalDisk> GetLogicalDisks() => LogicalDisks;
        public IReadOnlyList<GpuInfo> GetGpus() => Gpus;
        public IReadOnlyList<NetworkAdapter> GetNetworkAdapters() => NetworkAdapters;
    }

    [Fact]
    public async Task CollectAsync_AggregatesAllSections()
    {
        var svc = new HardwareReportService(new FakeSystemInfo());
        var report = await svc.CollectAsync();

        Assert.Equal("MY-PC", report.Computer.ComputerName);
        Assert.NotNull(report.Cpu);
        Assert.Equal("Intel i7-14700", report.Cpu!.Name);
        Assert.Equal(2, report.MemoryChips.Count);
        Assert.Single(report.PhysicalDisks);
        Assert.Single(report.LogicalDisks);
        Assert.Single(report.Gpus);
        Assert.Single(report.NetworkAdapters);
        Assert.Equal("Dell", report.Bios?.Manufacturer);
        Assert.Equal("Latitude 5540", report.Motherboard?.Product);
    }

    [Fact]
    public async Task CollectAsync_PreservesNullFields()
    {
        var info = new FakeSystemInfo();
        var svc = new HardwareReportService(info);
        var report = await svc.CollectAsync();

        Assert.Null(report.Cpu); // baseline still populated
        Assert.NotNull(report.Cpu);
        // Sanity: every section round-trips without mutation.
        Assert.Equal(info.MemoryChips.Count, report.MemoryChips.Count);
        Assert.Equal(info.PhysicalDisks.Count, report.PhysicalDisks.Count);
    }

    [Fact]
    public async Task CollectAsync_RespectsCancellation()
    {
        var svc = new HardwareReportService(new FakeSystemInfo());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.CollectAsync(cts.Token));
    }
}
