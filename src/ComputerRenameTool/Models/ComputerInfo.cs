namespace ComputerRenameTool.Models;

/// <summary>
/// Snapshot of the OS-level identity of the current machine.
/// Populated by <see cref="Services.ISystemInfoService.GetComputerInfo"/>.
/// </summary>
public sealed record ComputerInfo(
    string ComputerName,
    string WindowsVersion,
    string CurrentUser);
