using ComputerRenameTool.Models;
using ComputerRenameTool.MVVM;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// View-model for the full hardware report (summary + collapsible detail).
/// Surfaces every field as a formatted string so the XAML is dumb (FIX-REQUEST-7
/// §关键实现要求 5 — 12px 副文本 + 14px 主文本).
/// </summary>
public sealed class HardwareReportViewModel : ObservableObject
{
    private bool _isLoading;

    public HardwareReportViewModel(HardwareReport report)
    {
        Report = report;

        Cpu = CpuSummaryViewModel.From(report.Cpu);
        OperatingSystem = OsSummaryViewModel.From(report.OperatingSystem);
        Bios = BiosSummaryViewModel.From(report.Bios);
        Motherboard = MotherboardSummaryViewModel.From(report.Motherboard);

        MemoryChips = report.MemoryChips
            .Select(MemoryChipItemViewModel.From)
            .ToList();
        PhysicalDisks = report.PhysicalDisks
            .Select(PhysicalDiskItemViewModel.From)
            .ToList();
        LogicalDisks = report.LogicalDisks
            .Select(LogicalDiskItemViewModel.From)
            .ToList();
        Gpus = report.Gpus
            .Select(GpuItemViewModel.From)
            .ToList();
        NetworkAdapters = report.NetworkAdapters
            .Select(NetworkAdapterItemViewModel.From)
            .ToList();

        MemorySummary = BuildMemorySummary(report.MemoryChips, report.MemorySlotCount);
        DiskSummary = BuildDiskSummary(report.PhysicalDisks);
        NetworkSummary = BuildNetworkSummary(report.NetworkAdapters);
        PrimaryIpAddress = report.NetworkAdapters.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.IPv4Address))?.IPv4Address;
    }

    public HardwareReport Report { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public CpuSummaryViewModel Cpu { get; }
    public OsSummaryViewModel OperatingSystem { get; }
    public BiosSummaryViewModel Bios { get; }
    public MotherboardSummaryViewModel Motherboard { get; }

    public string MemorySummary { get; }
    public string DiskSummary { get; }
    public string NetworkSummary { get; }
    public string? PrimaryIpAddress { get; }

    public IReadOnlyList<MemoryChipItemViewModel> MemoryChips { get; }
    public IReadOnlyList<PhysicalDiskItemViewModel> PhysicalDisks { get; }
    public IReadOnlyList<LogicalDiskItemViewModel> LogicalDisks { get; }
    public IReadOnlyList<GpuItemViewModel> Gpus { get; }
    public IReadOnlyList<NetworkAdapterItemViewModel> NetworkAdapters { get; }

    public bool HasMemoryChips => MemoryChips.Count > 0;
    public bool HasPhysicalDisks => PhysicalDisks.Count > 0;
    public bool HasLogicalDisks => LogicalDisks.Count > 0;
    public bool HasGpus => Gpus.Count > 0;
    public bool HasNetworkAdapters => NetworkAdapters.Count > 0;
    public bool HasBios => Bios is not null && Bios.HasAnyValue;

    private static string BuildMemorySummary(IReadOnlyList<MemoryChip> chips, int? slotCount)
    {
        if (chips.Count == 0)
        {
            return "数据不可读";
        }

        // Group by capacity so a 2×32GB kit shows "2×32GB" (vs. "32GB + 32GB").
        // FIX-REQUEST-8: user feedback — the old "64 GB (2 × 32 GB)" read like
        // an arithmetic check; the slot count "(2/4 槽)" makes the count of
        // physical sticks obvious.
        var total = chips.Sum(c => (long)(c.CapacityBytes ?? 0UL));
        var totalGb = Math.Round(total / (1024d * 1024d * 1024d), 0);
        var perChip = chips
            .GroupBy(c => Math.Round((c.CapacityBytes ?? 0UL) / (1024d * 1024d * 1024d), 0))
            .OrderByDescending(g => g.Key)
            .Select(g => $"{g.Count()}×{g.Key:0}GB")
            .ToList();

        var modules = string.Join(" + ", perChip);
        var slotInfo = slotCount is null
            ? $"{chips.Count} 根"
            : $"{chips.Count}/{slotCount} 槽";

        return $"{totalGb:0} GB ({modules}, {slotInfo})";
    }

    private static string BuildNetworkSummary(IReadOnlyList<NetworkAdapter> adapters)
    {
        // FIX-REQUEST-8: the old summary used the first adapter's
        // NetConnectionID (e.g. "以太网 3") which is the *adapter friendly name*
        // not an IP. Pick the first adapter that actually has an IPv4 address.
        var withIp = adapters.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.IPv4Address));
        if (withIp is not null)
        {
            return withIp.IPv4Address!;
        }

        // No IP at all (Wi-Fi off, NIC disabled in software, etc.).
        if (adapters.Count == 0)
        {
            return "数据不可读";
        }
        return "无活动 IP";
    }

    private static string BuildDiskSummary(IReadOnlyList<PhysicalDisk> disks)
    {
        if (disks.Count == 0)
        {
            return "数据不可读";
        }

        return string.Join(" + ", disks.Select(d =>
        {
            var gb = d.SizeBytes is null ? "?" : $"{Math.Round(d.SizeBytes.Value / (1024d * 1024d * 1024d), 0)} GB";
            var iface = string.IsNullOrWhiteSpace(d.InterfaceType) ? "" : $" {d.InterfaceType}";
            return $"{gb}{iface}";
        }));
    }
}
