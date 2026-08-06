namespace ComputerRenameTool.Models;

/// <summary>
/// Single physical memory stick (one row per DIMM slot) from
/// <c>Win32_PhysicalMemory</c>. <see cref="CapacityBytes"/> is in bytes; the
/// caller formats for display. <see cref="SlotNumber"/> is derived from the
/// Win32_MemoryDevice <c>Tag</c> field (FIX-REQUEST-8 — memory summary
/// "2×32GB (2/4 槽)").
/// </summary>
public sealed record MemoryChip(
    string? DeviceLocator,
    string? Manufacturer,
    string? PartNumber,
    ulong? CapacityBytes,
    uint? SpeedMHz,
    ushort? FormFactor,
    int? SlotNumber);
