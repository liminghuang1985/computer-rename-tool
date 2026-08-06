namespace ComputerRenameTool.Models;

/// <summary>
/// Logical (mounted) disk from <c>Win32_LogicalDisk</c> with
/// <c>DriveType=3</c> (local disk). <see cref="DeviceId"/> is e.g. "C:".
/// </summary>
public sealed record LogicalDisk(
    string? DeviceId,
    string? VolumeName,
    ulong? SizeBytes,
    ulong? FreeSpaceBytes);
