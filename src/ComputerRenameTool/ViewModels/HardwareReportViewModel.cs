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
    private bool _isExpanded;

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

        MemorySummary = BuildMemorySummary(report.MemoryChips);
        DiskSummary = BuildDiskSummary(report.PhysicalDisks);
    }

    public HardwareReport Report { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandLabel));
            }
        }
    }

    public string ExpandLabel => _isExpanded ? "▾ 收起详细信息" : "▶ 展开详细信息";

    public CpuSummaryViewModel Cpu { get; }
    public OsSummaryViewModel OperatingSystem { get; }
    public BiosSummaryViewModel Bios { get; }
    public MotherboardSummaryViewModel Motherboard { get; }

    public string MemorySummary { get; }
    public string DiskSummary { get; }

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

    private static string BuildMemorySummary(IReadOnlyList<MemoryChip> chips)
    {
        if (chips.Count == 0)
        {
            return "数据不可读";
        }

        var total = chips.Sum(c => c.CapacityBytes ?? 0UL);
        var totalGb = Math.Round(total / (1024d * 1024d * 1024d), 1);
        var modules = string.Join(" + ", chips.Select(c =>
        {
            var gb = c.CapacityBytes is null ? "?" : $"{Math.Round(c.CapacityBytes.Value / (1024d * 1024d * 1024d), 0)} GB";
            return gb;
        }));
        return $"{totalGb:0.#} GB ({chips.Count} × {modules})";
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
