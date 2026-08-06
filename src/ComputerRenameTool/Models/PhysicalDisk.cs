namespace ComputerRenameTool.Models;

/// <summary>
/// Physical disk descriptor from <c>Win32_DiskDrive</c>. Capacity is in bytes;
/// interface is "NVMe" / "SATA" / "SCSI" / etc., and status is "OK" / "Degraded"
/// etc. (WMI-defined strings).
/// </summary>
public sealed record PhysicalDisk(
    string? Model,
    ulong? SizeBytes,
    string? InterfaceType,
    string? Status,
    string? SerialNumber);
