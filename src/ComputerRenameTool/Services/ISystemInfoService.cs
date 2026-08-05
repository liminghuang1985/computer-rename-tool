using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Source of OS identity and hardware descriptors surfaced in the main window.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>Returns the current machine name, Windows version, and logged-in user.</summary>
    ComputerInfo GetComputerInfo();

    /// <summary>
    /// Returns CPU / memory / GPU / disk descriptors. Individual fields may be
    /// <c>null</c> when their source is unavailable; callers must render
    /// "未知 (...)" in that case rather than throw.
    /// </summary>
    HardwareInfo GetHardwareInfo();
}
