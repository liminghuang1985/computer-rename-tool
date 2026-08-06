using ComputerRenameTool.Models;
using ComputerRenameTool.ViewModels;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Tests for the HardwareReportViewModel aggregation. The VM takes a fully
/// populated <see cref="HardwareReport"/> and must render every section as
/// either a real value or "数据不可读" — never throw or blank the whole
/// report (FIX-REQUEST-7 §关键实现要求 1).
/// </summary>
public class HardwareReportViewModelTests
{
    private static HardwareReport FullReport() => new(
        new ComputerInfo("MY-PC", "Windows 11 Pro", "alice"),
        new CpuInfo("Intel i7-14700", 20, 28, 5.4, (ushort)23),
        new OperatingSystemInfo("Windows 11 Pro", "10.0", "22631", "20240101000000.000000+000", "20260804090012.123456+000", "OS-SN", 0, 0),
        new BiosInfo("Dell", "1.5", "20240115000000.000000+000"),
        new MotherboardInfo("Dell Inc.", "Latitude 5540", "ABC123"),
        new[]
        {
            new MemoryChip("DIMM A1", "Samsung", "M471A2K43", 16UL * 1024 * 1024 * 1024, 4800, 13, 1),
            new MemoryChip("DIMM B1", "Hynix",  "HMCG78ME", 16UL * 1024 * 1024 * 1024, 4800, 13, 3),
        },
        MemorySlotCount: 4,
        new[]
        {
            new PhysicalDisk("Samsung 990 PRO", 1024UL * 1024 * 1024 * 1024, "NVMe", "OK", "S123"),
        },
        new[]
        {
            new LogicalDisk("C:", "System", 500UL * 1024 * 1024 * 1024, 200UL * 1024 * 1024 * 1024),
        },
        new[]
        {
            new GpuInfo("NVIDIA RTX 4060", "31.0.15.4601", 8UL * 1024 * 1024 * 1024, "AD107"),
        },
        new[]
        {
            new NetworkAdapter("Wi-Fi", "WiFi", "aa:bb:cc:dd:ee:ff", 1_000_000_000UL, "10.12.138.38", "255.255.255.0", "10.12.138.1"),
        });

    [Fact]
    public void Constructor_PopulatesEverySection()
    {
        var vm = new HardwareReportViewModel(FullReport());

        Assert.Equal("Intel i7-14700", vm.Cpu.DisplayName);
        Assert.Equal("20 核 / 28 线程", vm.Cpu.CoresLogicalDisplay);
        Assert.Equal("5.4 GHz", vm.Cpu.MaxClockDisplay);
        Assert.Equal("23%", vm.Cpu.LoadDisplay);
        Assert.Equal("Windows 11 Pro", vm.OperatingSystem.DisplayName);
        Assert.Equal("Dell 1.5 20240115", vm.Bios.DisplayName);
        Assert.Equal("Dell Inc. Latitude 5540 ABC123", vm.Motherboard.DisplayName);
        Assert.Equal(2, vm.MemoryChips.Count);
        Assert.Equal("16 GB", vm.MemoryChips[0].Capacity);
        Assert.Equal("Samsung", vm.MemoryChips[0].Manufacturer);
        Assert.Equal("4800 MHz", vm.MemoryChips[0].Speed);
        Assert.Equal("NVMe", vm.PhysicalDisks[0].Interface);
        Assert.Equal("C:", vm.LogicalDisks[0].DeviceId);
        Assert.Equal("60%", vm.LogicalDisks[0].Percent);
        Assert.Equal("NVIDIA RTX 4060", vm.Gpus[0].Name);
        Assert.Equal("8 GB", vm.Gpus[0].VideoMemory);
        Assert.Equal("WiFi", vm.NetworkAdapters[0].Connection);
        Assert.Equal("1 Gbps", vm.NetworkAdapters[0].Speed);
    }

    [Fact]
    public void MemorySummary_CombinesChipCount()
    {
        // FIX-REQUEST-8: per-chip grouping "2×16GB" + slot count "(2/4 槽)"
        // so users see the physical stick layout at a glance, not just total GB.
        var vm = new HardwareReportViewModel(FullReport());
        Assert.Equal("32 GB (2×16GB, 2/4 槽)", vm.MemorySummary);
    }

    [Fact]
    public void NetworkSummary_PrefersAdapterWithIPv4()
    {
        // User feedback: the old summary used NetConnectionID ("以太网 3")
        // which is the friendly adapter name, not an IP. The summary must
        // pick the first adapter that actually has an IPv4 (FIX-REQUEST-8).
        var report = new HardwareReport(
            new ComputerInfo("PC", "Win", "u"),
            null, null, null, null,
            Array.Empty<MemoryChip>(),
            MemorySlotCount: null,
            Array.Empty<PhysicalDisk>(),
            Array.Empty<LogicalDisk>(),
            Array.Empty<GpuInfo>(),
            new[]
            {
                new NetworkAdapter("WAN Miniport", "以太网 3", null, null, null, null, null),
                new NetworkAdapter("Intel Wi-Fi 6", "WiFi", "aa:bb:cc:dd:ee:ff", 1_000_000_000UL,
                    "10.12.138.38", "255.255.255.0", "10.12.138.1"),
            });
        var vm = new HardwareReportViewModel(report);
        Assert.Equal("10.12.138.38", vm.NetworkSummary);
        Assert.Equal("10.12.138.38", vm.PrimaryIpAddress);
    }

    [Fact]
    public void DiskSummary_ListsEachDisk()
    {
        var vm = new HardwareReportViewModel(FullReport());
        Assert.Equal("1024 GB NVMe", vm.DiskSummary);
    }

    [Fact]
    public void EmptyReport_RendersDataUnreadable()
    {
        var vm = new HardwareReportViewModel(HardwareReport.Empty(new ComputerInfo("X", "Y", "Z")));
        Assert.Equal("数据不可读", vm.Cpu.DisplayName);
        Assert.Equal("数据不可读", vm.MemorySummary);
        Assert.Equal("数据不可读", vm.DiskSummary);
        Assert.False(vm.HasMemoryChips);
        Assert.False(vm.HasPhysicalDisks);
        Assert.False(vm.HasNetworkAdapters);
    }

    [Fact]
    public void HasFlags_ReflectCollectionSize()
    {
        var vm = new HardwareReportViewModel(FullReport());
        Assert.True(vm.HasMemoryChips);
        Assert.True(vm.HasPhysicalDisks);
        Assert.True(vm.HasLogicalDisks);
        Assert.True(vm.HasGpus);
        Assert.True(vm.HasNetworkAdapters);
        Assert.True(vm.HasBios);
    }
}
