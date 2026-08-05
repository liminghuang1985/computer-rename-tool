using ComputerRenameTool.Models;
using ComputerRenameTool.MVVM;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// View-model for the "hardware info" section. Renders each field with the
/// "未知 (...)" fallback rather than the empty string, per DESIGN.md §4.1.
/// </summary>
public sealed class HardwareInfoViewModel : ObservableObject
{
    private const string UnknownPlaceholder = "未知 (驱动未安装)";

    public HardwareInfoViewModel(HardwareInfo info)
    {
        Cpu = string.IsNullOrWhiteSpace(info.Cpu) ? UnknownPlaceholder : info.Cpu;
        Memory = string.IsNullOrWhiteSpace(info.Memory) ? UnknownPlaceholder : info.Memory;
        Gpu = string.IsNullOrWhiteSpace(info.Gpu) ? UnknownPlaceholder : info.Gpu;
        Disk = string.IsNullOrWhiteSpace(info.Disk) ? UnknownPlaceholder : info.Disk;
    }

    public string Cpu { get; }
    public string Memory { get; }
    public string Gpu { get; }
    public string Disk { get; }
}
