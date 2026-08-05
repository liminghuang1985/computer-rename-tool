namespace ComputerRenameTool.Models;

/// <summary>
/// Snapshot of the visible hardware. Any individual field may be <c>null</c>
/// when the WMI provider could not resolve it — the UI is expected to render
/// "未知 (...)" in that case rather than blow up.
/// </summary>
public sealed record HardwareInfo(
    string? Cpu,
    string? Memory,
    string? Gpu,
    string? Disk)
{
    /// <summary>
    /// Returns a HardwareInfo where every field is the same placeholder. Used as
    /// a fallback when even WMI itself is unavailable so the view-model can
    /// still bind and the user sees a clean "WMI 服务未启动" message.
    /// </summary>
    public static HardwareInfo AllUnknown(string reason) => new(reason, reason, reason, reason);
}
