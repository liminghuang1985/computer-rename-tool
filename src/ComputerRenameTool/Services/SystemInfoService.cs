using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ComputerRenameTool.Models;
using Microsoft.Win32;

namespace ComputerRenameTool.Services;

/// <summary>
/// Reads OS identity and hardware descriptors from Win32 sources — no WMI, no
/// third-party NuGet packages, in line with DESIGN.md §11. Each field is read
/// independently so a single failure does not blank out the others (DESIGN.md
/// §4.1, "未知 (...)" rendering).
/// </summary>
public sealed class SystemInfoService : ISystemInfoService
{
    public ComputerInfo GetComputerInfo()
    {
        var name = SafeRead(() => Environment.MachineName, "未知");
        var os = SafeRead(ReadWindowsVersion, "未知 Windows");
        var user = SafeRead(() => Environment.UserName, "未知");
        return new ComputerInfo(name, os, user);
    }

    public HardwareInfo GetHardwareInfo()
    {
        var cpu = SafeRead(ReadCpuName, null);
        var memory = SafeRead(ReadTotalMemory, null);
        var gpu = SafeRead(ReadGpuName, null);
        var disk = SafeRead(ReadSystemDisk, null);
        return new HardwareInfo(cpu, memory, gpu, disk);
    }

    private static string ReadWindowsVersion()
    {
        // Prefer the registry product name (e.g. "Windows 11 Pro").
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string;
            var displayVer = key?.GetValue("DisplayVersion") as string;
            if (!string.IsNullOrWhiteSpace(product))
            {
                return string.IsNullOrWhiteSpace(displayVer)
                    ? product!
                    : $"{product} {displayVer}";
            }
        }
        catch
        {
            // fall through
        }

        return Environment.OSVersion.VersionString;
    }

    private static string? ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        catch
        {
            // ignored
        }
        return null;
    }

    private static string? ReadTotalMemory()
    {
        try
        {
            var status = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref status))
            {
                var gb = status.ullTotalPhys / (1024d * 1024d * 1024d);
                return $"{Math.Round(gb, 1):0.#} GB";
            }
        }
        catch
        {
            // fall through
        }
        return null;
    }

    private static string? ReadGpuName()
    {
        // Walk the PCI device tree looking for Display controllers (class 0x03).
        try
        {
            string? discrete = null;
            string? any = null;

            using var pci = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
            if (pci is null)
            {
                return null;
            }

            foreach (var deviceKeyName in pci.GetSubKeyNames())
            {
                using var deviceKey = pci.OpenSubKey(deviceKeyName);
                if (deviceKey is null)
                {
                    continue;
                }

                foreach (var instanceKeyName in deviceKey.GetSubKeyNames())
                {
                    using var instanceKey = deviceKey.OpenSubKey(instanceKeyName);
                    var classGuid = instanceKey?.GetValue("ClassGuid") as string;
                    if (!string.Equals(classGuid, "{4d36e968-e325-11ce-bfc1-08002be10318}",
                                       StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = instanceKey!.GetValue("DeviceDesc") as string;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // DeviceDesc is "@\\%SystemRoot%\\...\\device.dll,-xxx;Friendly Name".
                    var friendly = ExtractFriendlyName(name!);
                    if (string.IsNullOrWhiteSpace(friendly))
                    {
                        continue;
                    }

                    if (IsDiscreteGpu(friendly))
                    {
                        discrete = friendly;
                        break;
                    }
                    any ??= friendly;
                }

                if (discrete is not null)
                {
                    break;
                }
            }

            return discrete ?? any;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractFriendlyName(string deviceDesc)
    {
        var semi = deviceDesc.LastIndexOf(';');
        return semi >= 0 && semi + 1 < deviceDesc.Length
            ? deviceDesc[(semi + 1)..]
            : deviceDesc;
    }

    private static bool IsDiscreteGpu(string name)
    {
        // Anything labelled "Intel" or matching an integrated AMD APU is treated as iGPU.
        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // AMD integrated: Radeon Graphics without an RX/Radeon Pro/Vega product suffix.
        if (name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
            && !RegexHasRadeonDiscreteMarker(name))
        {
            return false;
        }

        return true;
    }

    private static bool RegexHasRadeonDiscreteMarker(string name)
    {
        return name.Contains("RX ", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Radeon Pro", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Vega", StringComparison.OrdinalIgnoreCase)
            || name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Quadro", StringComparison.OrdinalIgnoreCase)
            || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GTX", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadSystemDisk()
    {
        // Resolve the system drive and surface its total size + volume label.
        try
        {
            var sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sysDrive);
            if (!drive.IsReady)
            {
                return drive.Name;
            }

            var sizeGb = drive.TotalSize / (1024d * 1024d * 1024d);
            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "系统盘" : drive.VolumeLabel;
            return $"{label} ({sizeGb:0.#} GB)";
        }
        catch
        {
            return null;
        }
    }

    private static T SafeRead<T>(Func<T> reader, T fallback)
    {
        try
        {
            return reader();
        }
        catch (Exception ex)
        {
            App.Logger?.Warn($"SystemInfo field read failed: {ex.Message}");
            return fallback;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            dwMemoryLoad = 0;
            ullTotalPhys = 0;
            ullAvailPhys = 0;
            ullTotalPageFile = 0;
            ullAvailPageFile = 0;
            ullTotalVirtual = 0;
            ullAvailVirtual = 0;
            ullAvailExtendedVirtual = 0;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
