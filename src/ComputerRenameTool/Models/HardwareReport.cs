namespace ComputerRenameTool.Models;

/// <summary>
/// Aggregate report returned by <see cref="Services.IHardwareReportService.CollectAsync"/>.
/// Each field is independently nullable so a single WMI failure does not blank
/// the rest of the report. The UI renders "数据不可读" for missing fields.
/// </summary>
public sealed record HardwareReport(
    ComputerInfo Computer,
    CpuInfo? Cpu,
    OperatingSystemInfo? OperatingSystem,
    BiosInfo? Bios,
    MotherboardInfo? Motherboard,
    IReadOnlyList<MemoryChip> MemoryChips,
    IReadOnlyList<PhysicalDisk> PhysicalDisks,
    IReadOnlyList<LogicalDisk> LogicalDisks,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<NetworkAdapter> NetworkAdapters)
{
    public static HardwareReport Empty(ComputerInfo computer) =>
        new(computer, null, null, null, null,
            Array.Empty<MemoryChip>(),
            Array.Empty<PhysicalDisk>(),
            Array.Empty<LogicalDisk>(),
            Array.Empty<GpuInfo>(),
            Array.Empty<NetworkAdapter>());
}
