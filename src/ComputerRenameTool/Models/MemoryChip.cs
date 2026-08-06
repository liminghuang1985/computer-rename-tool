namespace ComputerRenameTool.Models;

/// <summary>
/// Single physical memory stick (one row per DIMM slot) from
/// <c>Win32_PhysicalMemory</c>. <see cref="CapacityBytes"/> is in bytes; the
/// caller formats for display.
/// </summary>
public sealed record MemoryChip(
    string? DeviceLocator,
    string? Manufacturer,
    string? PartNumber,
    ulong? CapacityBytes,
    uint? SpeedMHz,
    ushort? FormFactor);
