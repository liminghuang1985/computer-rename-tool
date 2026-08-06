using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Aggregates every WMI hardware read into a single <see cref="HardwareReport"/>.
/// Calls are serialised on a background thread so the WMI provider is never
/// hit concurrently (FIX-REQUEST-7 §关键实现要求 2).
/// </summary>
public interface IHardwareReportService
{
    Task<HardwareReport> CollectAsync(CancellationToken cancellationToken = default);
}
