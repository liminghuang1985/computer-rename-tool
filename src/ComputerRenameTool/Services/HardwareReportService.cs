using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Default <see cref="IHardwareReportService"/> implementation. Runs the WMI
/// reads on a worker thread and serialises them so the CIM provider is never
/// hit concurrently.
/// </summary>
public sealed class HardwareReportService : IHardwareReportService
{
    private readonly ISystemInfoService _systemInfo;

    public HardwareReportService() : this(new SystemInfoService())
    {
    }

    public HardwareReportService(ISystemInfoService systemInfo)
    {
        _systemInfo = systemInfo;
    }

    public Task<HardwareReport> CollectAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var computer = _systemInfo.GetComputerInfo();
            var cpu = _systemInfo.GetCpuInfo();
            var os = _systemInfo.GetOperatingSystemInfo();
            var bios = _systemInfo.GetBiosInfo();
            var mb = _systemInfo.GetMotherboardInfo();
            var memory = _systemInfo.GetMemoryChips();
            var physical = _systemInfo.GetPhysicalDisks();
            var logical = _systemInfo.GetLogicalDisks();
            var gpus = _systemInfo.GetGpus();
            var network = _systemInfo.GetNetworkAdapters();

            return new HardwareReport(computer, cpu, os, bios, mb, memory, physical, logical, gpus, network);
        }, cancellationToken);
    }
}
