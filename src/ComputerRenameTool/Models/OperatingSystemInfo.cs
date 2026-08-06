namespace ComputerRenameTool.Models;

/// <summary>
/// Detailed OS descriptor from <c>Win32_OperatingSystem</c>. <see cref="InstallDate"/>
/// and <see cref="LastBootUpTime"/> are WMI date strings (DMTF format) and
/// should be parsed by the service if a <see cref="DateTime"/> is needed.
/// </summary>
public sealed record OperatingSystemInfo(
    string? Caption,
    string? Version,
    string? BuildNumber,
    string? InstallDate,
    string? LastBootUpTime,
    string? SerialNumber,
    ulong? TotalVisibleMemoryBytes,
    ulong? FreePhysicalMemoryBytes);
