using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using ComputerRenameTool.Models;
using Microsoft.Win32;

namespace ComputerRenameTool.Services;

/// <summary>
/// Reads OS identity and full hardware descriptors from WMI
/// (<c>Win32_*</c> CIM classes). Falls back to the registry / Win32 for the
/// legacy "summary" fields so existing tests and the simple one-line summary
/// still work even when WMI is unavailable. Each WMI call is wrapped so a
/// single failure does not blank out the rest of the report (FIX-REQUEST-7
/// §关键实现要求 1).
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

    public CpuInfo? GetCpuInfo()
    {
        return SafeRead(() =>
        {
            using var searcher = NewWmiSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, LoadPercentage FROM Win32_Processor");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    var name = obj["Name"] as string;
                    var cores = ToInt(obj["NumberOfCores"]);
                    var logical = ToInt(obj["NumberOfLogicalProcessors"]);
                    var maxMhz = ToLong(obj["MaxClockSpeed"]);
                    var load = ToUShort(obj["LoadPercentage"]);
                    return new CpuInfo(
                        TrimCpuName(name),
                        cores,
                        logical,
                        maxMhz is null ? null : Math.Round(maxMhz.Value / 1000d, 2),
                        load);
                }
            }
            return null;
        }, null);
    }

    public OperatingSystemInfo? GetOperatingSystemInfo()
    {
        return SafeRead(() =>
        {
            using var searcher = NewWmiSearcher("SELECT Caption, Version, BuildNumber, InstallDate, LastBootUpTime, SerialNumber, TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    return new OperatingSystemInfo(
                        obj["Caption"] as string,
                        obj["Version"] as string,
                        obj["BuildNumber"] as string,
                        obj["InstallDate"] as string,
                        obj["LastBootUpTime"] as string,
                        obj["SerialNumber"] as string,
                        ToULong(obj["TotalVisibleMemorySize"]) * 1024UL,
                        ToULong(obj["FreePhysicalMemory"]) * 1024UL);
                }
            }
            return null;
        }, null);
    }

    public BiosInfo? GetBiosInfo()
    {
        return SafeRead(() =>
        {
            using var searcher = NewWmiSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    return new BiosInfo(
                        obj["Manufacturer"] as string,
                        obj["SMBIOSBIOSVersion"] as string,
                        NormalizeWmiDate(obj["ReleaseDate"] as string));
                }
            }
            return null;
        }, null);
    }

    public MotherboardInfo? GetMotherboardInfo()
    {
        return SafeRead(() =>
        {
            using var searcher = NewWmiSearcher("SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    return new MotherboardInfo(
                        obj["Manufacturer"] as string,
                        obj["Product"] as string,
                        obj["SerialNumber"] as string);
                }
            }
            return null;
        }, null);
    }

    public IReadOnlyList<MemoryChip> GetMemoryChips()
    {
        return SafeRead(() =>
        {
            var list = new List<MemoryChip>();
            using var searcher = NewWmiSearcher("SELECT DeviceLocator, Manufacturer, PartNumber, Capacity, Speed, FormFactor FROM Win32_PhysicalMemory");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    list.Add(new MemoryChip(
                        obj["DeviceLocator"] as string,
                        obj["Manufacturer"] as string,
                        obj["PartNumber"] as string,
                        ToULong(obj["Capacity"]),
                        ToUInt(obj["Speed"]),
                        ToUShort(obj["FormFactor"])));
                }
            }
            return (IReadOnlyList<MemoryChip>)list;
        }, Array.Empty<MemoryChip>());
    }

    public IReadOnlyList<PhysicalDisk> GetPhysicalDisks()
    {
        return SafeRead(() =>
        {
            // Index=0 keeps the boot drive first in the list — easier to read.
            var list = new List<PhysicalDisk>();
            using var searcher = NewWmiSearcher("SELECT Model, Size, InterfaceType, Status, SerialNumber, Index FROM Win32_DiskDrive");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    list.Add(new PhysicalDisk(
                        TrimCpuName(obj["Model"] as string),
                        ToULong(obj["Size"]),
                        obj["InterfaceType"] as string,
                        obj["Status"] as string,
                        obj["SerialNumber"] as string));
                }
            }
            list.Sort((a, b) => string.Compare(a.Model, b.Model, StringComparison.OrdinalIgnoreCase));
            return (IReadOnlyList<PhysicalDisk>)list;
        }, Array.Empty<PhysicalDisk>());
    }

    public IReadOnlyList<LogicalDisk> GetLogicalDisks()
    {
        return SafeRead(() =>
        {
            var list = new List<LogicalDisk>();
            using var searcher = NewWmiSearcher("SELECT DeviceID, VolumeName, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    list.Add(new LogicalDisk(
                        obj["DeviceID"] as string,
                        obj["VolumeName"] as string,
                        ToULong(obj["Size"]),
                        ToULong(obj["FreeSpace"])));
                }
            }
            list.Sort((a, b) => string.Compare(a.DeviceId, b.DeviceId, StringComparison.Ordinal));
            return (IReadOnlyList<LogicalDisk>)list;
        }, Array.Empty<LogicalDisk>());
    }

    public IReadOnlyList<GpuInfo> GetGpus()
    {
        return SafeRead(() =>
        {
            var list = new List<GpuInfo>();
            using var searcher = NewWmiSearcher("SELECT Name, DriverVersion, AdapterRAM, VideoProcessor FROM Win32_VideoController");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    list.Add(new GpuInfo(
                        obj["Name"] as string,
                        obj["DriverVersion"] as string,
                        ToULong(obj["AdapterRAM"]),
                        obj["VideoProcessor"] as string));
                }
            }
            return (IReadOnlyList<GpuInfo>)list;
        }, Array.Empty<GpuInfo>());
    }

    public IReadOnlyList<NetworkAdapter> GetNetworkAdapters()
    {
        return SafeRead(() =>
        {
            var list = new List<NetworkAdapter>();
            using var searcher = NewWmiSearcher("SELECT Name, MACAddress, NetConnectionID, Speed, NetEnabled FROM Win32_NetworkAdapter WHERE NetEnabled=TRUE");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject obj in collection)
            {
                using (obj)
                {
                    list.Add(new NetworkAdapter(
                        obj["Name"] as string,
                        obj["NetConnectionID"] as string,
                        obj["MACAddress"] as string,
                        ToULong(obj["Speed"])));
                }
            }
            return (IReadOnlyList<NetworkAdapter>)list;
        }, Array.Empty<NetworkAdapter>());
    }

    // ----------------------------------------------------------------------
    // Legacy registry / Win32 helpers (kept for the summary one-liners).
    // ----------------------------------------------------------------------

    private static string ReadWindowsVersion()
    {
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
        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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

    // ----------------------------------------------------------------------
    // WMI plumbing helpers.
    // ----------------------------------------------------------------------

    private static ManagementObjectSearcher NewWmiSearcher(string query)
    {
        // Use a per-call options object so the WMI COM apartment is created
        // inside the calling thread (the searcher is disposed before the
        // thread returns, which sidesteps cross-apartment marshal errors).
        var options = new ConnectionOptions
        {
            Impersonation = ImpersonationLevel.Impersonate,
            Authentication = AuthenticationLevel.Default,
            EnablePrivileges = true,
        };
        var scope = new ManagementScope(@"\\.\root\cimv2", options);
        return new ManagementObjectSearcher(scope, new ObjectQuery(query));
    }

    /// <summary>WMI DMTF datetime like "20240115000000.000000+000" → "2024-01-15".</summary>
    private static string? NormalizeWmiDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw!.Length < 8) return raw;
        return $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}";
    }

    private static string? TrimCpuName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw!.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static int? ToInt(object? value) => value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    private static uint? ToUInt(object? value) => value is null ? null : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
    private static long? ToLong(object? value) => value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    private static ulong? ToULong(object? value) => value is null ? null : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    private static ushort? ToUShort(object? value) => value is null ? null : Convert.ToUInt16(value, CultureInfo.InvariantCulture);

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
