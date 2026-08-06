using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Source of OS identity and hardware descriptors surfaced in the main window.
/// Individual fields may be <c>null</c> / empty when the WMI provider cannot
/// resolve them — the caller is expected to render "数据不可读" rather than
/// substitute a placeholder (FIX-REQUEST-7 §关键实现要求 1).
/// </summary>
public interface ISystemInfoService
{
    ComputerInfo GetComputerInfo();

    HardwareInfo GetHardwareInfo();

    CpuInfo? GetCpuInfo();

    OperatingSystemInfo? GetOperatingSystemInfo();

    BiosInfo? GetBiosInfo();

    MotherboardInfo? GetMotherboardInfo();

    IReadOnlyList<MemoryChip> GetMemoryChips();

    /// <summary>
    /// Total DIMM slots on the motherboard (e.g. 4 for a typical desktop) from
    /// <c>Win32_PhysicalMemoryArray.MemoryDevices</c>. Returns <c>null</c> when
    /// WMI cannot resolve it (FIX-REQUEST-8).
    /// </summary>
    int? GetMemorySlotCount();

    IReadOnlyList<PhysicalDisk> GetPhysicalDisks();

    IReadOnlyList<LogicalDisk> GetLogicalDisks();

    IReadOnlyList<GpuInfo> GetGpus();

    IReadOnlyList<NetworkAdapter> GetNetworkAdapters();
}
