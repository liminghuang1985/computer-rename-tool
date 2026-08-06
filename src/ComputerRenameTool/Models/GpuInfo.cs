namespace ComputerRenameTool.Models;

/// <summary>
/// Display adapter descriptor from <c>Win32_VideoController</c>.
/// <see cref="AdapterRamBytes"/> is the (often inaccurate) WMI-reported VRAM
/// in bytes; we use it as a best-effort estimate since the Win32 API does not
/// expose a more reliable field without DXGI.
/// </summary>
public sealed record GpuInfo(
    string? Name,
    string? DriverVersion,
    ulong? AdapterRamBytes,
    string? VideoProcessor);
