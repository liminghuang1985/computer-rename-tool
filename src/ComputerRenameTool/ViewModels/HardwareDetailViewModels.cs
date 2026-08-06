using ComputerRenameTool.Models;
using ComputerRenameTool.MVVM;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// One-line CPU summary for the collapsed section. Returns "数据不可读" when
/// the WMI provider returned no CPU at all (FIX-REQUEST-7 §关键实现要求 1).
/// </summary>
public sealed class CpuSummaryViewModel
{
    public static readonly CpuSummaryViewModel Empty = new(null, null, null, null, null);

    public static CpuSummaryViewModel From(CpuInfo? info)
    {
        if (info is null) return Empty;
        return new CpuSummaryViewModel(info.Name, info.NumberOfCores, info.NumberOfLogicalProcessors,
            info.MaxClockSpeedGHz, info.LoadPercentage);
    }

    private CpuSummaryViewModel(string? name, int? cores, int? logical, double? maxGhz, ushort? load)
    {
        DisplayName = string.IsNullOrWhiteSpace(name) ? "数据不可读" : name.Trim();
        Cores = cores;
        Logical = logical;
        MaxClockGHz = maxGhz;
        Load = load;
    }

    public string DisplayName { get; }
    public int? Cores { get; }
    public int? Logical { get; }
    public double? MaxClockGHz { get; }
    public ushort? Load { get; }

    public string CoresLogicalDisplay => Cores is null || Logical is null
        ? "数据不可读"
        : $"{Cores} 核 / {Logical} 线程";

    public string MaxClockDisplay => MaxClockGHz is null ? "数据不可读" : $"{MaxClockGHz:0.##} GHz";
    public string LoadDisplay => Load is null ? "数据不可读" : $"{Load}%";
    public bool HasAnyValue => !string.IsNullOrWhiteSpace(DisplayName) && DisplayName != "数据不可读";
}

/// <summary>OS summary line — includes install date / boot time when known.</summary>
public sealed class OsSummaryViewModel
{
    public static readonly OsSummaryViewModel Empty = new(null, null, null, null);

    public static OsSummaryViewModel From(OperatingSystemInfo? info)
    {
        if (info is null) return Empty;
        return new OsSummaryViewModel(info.Caption, info.InstallDate, info.LastBootUpTime, info.BuildNumber);
    }

    private OsSummaryViewModel(string? caption, string? install, string? boot, string? build)
    {
        DisplayName = string.IsNullOrWhiteSpace(caption) ? "数据不可读" : caption.Trim();
        InstallDate = NormalizeDate(install);
        BootTime = NormalizeDate(boot);
        BuildNumber = build;
    }

    public string DisplayName { get; }
    public string? InstallDate { get; }
    public string? BootTime { get; }
    public string? BuildNumber { get; }

    public bool HasAnyValue => !string.IsNullOrWhiteSpace(DisplayName) && DisplayName != "数据不可读";

    private static string? NormalizeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw!.Length < 8) return raw;
        return $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)}";
    }
}

/// <summary>BIOS summary — manufacturer + version + release date.</summary>
public sealed class BiosSummaryViewModel
{
    public static readonly BiosSummaryViewModel Empty = new(null, null, null);

    public static BiosSummaryViewModel From(BiosInfo? info)
    {
        if (info is null) return Empty;
        return new BiosSummaryViewModel(info.Manufacturer, info.SmbiosVersion, info.ReleaseDate);
    }

    private BiosSummaryViewModel(string? manufacturer, string? version, string? releaseDate)
    {
        Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        ReleaseDate = string.IsNullOrWhiteSpace(releaseDate) ? null : releaseDate;
        DisplayName = string.Join(" ", new[] { Manufacturer, Version, ReleaseDate }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public string? Manufacturer { get; }
    public string? Version { get; }
    public string? ReleaseDate { get; }
    public string DisplayName { get; }

    public bool HasAnyValue => !string.IsNullOrWhiteSpace(DisplayName);
}

/// <summary>Motherboard summary — manufacturer + product + serial.</summary>
public sealed class MotherboardSummaryViewModel
{
    public static readonly MotherboardSummaryViewModel Empty = new(null, null, null);

    public static MotherboardSummaryViewModel From(MotherboardInfo? info)
    {
        if (info is null) return Empty;
        return new MotherboardSummaryViewModel(info.Manufacturer, info.Product, info.SerialNumber);
    }

    private MotherboardSummaryViewModel(string? manufacturer, string? product, string? serial)
    {
        Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim();
        Product = string.IsNullOrWhiteSpace(product) ? null : product.Trim();
        SerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim();
        DisplayName = string.Join(" ", new[] { Manufacturer, Product, SerialNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public string? Manufacturer { get; }
    public string? Product { get; }
    public string? SerialNumber { get; }
    public string DisplayName { get; }

    public bool HasAnyValue => !string.IsNullOrWhiteSpace(DisplayName);
}

/// <summary>One row in the memory table.</summary>
public sealed class MemoryChipItemViewModel
{
    public static MemoryChipItemViewModel From(MemoryChip c) => new(c);

    private MemoryChipItemViewModel(MemoryChip c)
    {
        Locator = c.DeviceLocator ?? "—";
        Manufacturer = c.Manufacturer ?? "—";
        PartNumber = c.PartNumber ?? "—";
        Capacity = c.CapacityBytes is null
            ? "—"
            : $"{Math.Round(c.CapacityBytes.Value / (1024d * 1024d * 1024d), 1):0.#} GB";
        Speed = c.SpeedMHz is null ? "—" : $"{c.SpeedMHz} MHz";
        FormFactor = MapFormFactor(c.FormFactor);
    }

    public string Locator { get; }
    public string Manufacturer { get; }
    public string PartNumber { get; }
    public string Capacity { get; }
    public string Speed { get; }
    public string FormFactor { get; }

    private static string MapFormFactor(ushort? code) => code switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "SIMM",
        4 => "SIP",
        5 => "Chip",
        6 => "DIP",
        7 => "ZIP",
        8 => "Proprietary Card",
        9 => "DIMM",
        10 => "TSOP",
        11 => "Row Of Chips",
        12 => "RIMM",
        13 => "SODIMM",
        14 => "SRIMM",
        15 => "FB-DIMM",
        _ => "—",
    };
}

/// <summary>One row in the physical disk table.</summary>
public sealed class PhysicalDiskItemViewModel
{
    public static PhysicalDiskItemViewModel From(PhysicalDisk d) => new(d);

    private PhysicalDiskItemViewModel(PhysicalDisk d)
    {
        Model = string.IsNullOrWhiteSpace(d.Model) ? "—" : d.Model.Trim();
        Size = d.SizeBytes is null
            ? "—"
            : $"{Math.Round(d.SizeBytes.Value / (1024d * 1024d * 1024d), 1):0.#} GB";
        Interface = string.IsNullOrWhiteSpace(d.InterfaceType) ? "—" : d.InterfaceType;
        Status = string.IsNullOrWhiteSpace(d.Status) ? "—" : d.Status;
    }

    public string Model { get; }
    public string Size { get; }
    public string Interface { get; }
    public string Status { get; }
}

/// <summary>One row in the logical disk table.</summary>
public sealed class LogicalDiskItemViewModel
{
    public static LogicalDiskItemViewModel From(LogicalDisk d) => new(d);

    private LogicalDiskItemViewModel(LogicalDisk d)
    {
        DeviceId = d.DeviceId ?? "—";
        Label = string.IsNullOrWhiteSpace(d.VolumeName) ? "(无标签)" : d.VolumeName;
        Size = d.SizeBytes is null ? "—" : $"{Math.Round(d.SizeBytes.Value / (1024d * 1024d * 1024d), 1):0.#} GB";
        Used = d.SizeBytes is null || d.FreeSpaceBytes is null
            ? "—"
            : $"{Math.Round((d.SizeBytes.Value - d.FreeSpaceBytes.Value) / (1024d * 1024d * 1024d), 1):0.#} GB";
        Percent = d.SizeBytes is null || d.FreeSpaceBytes is null || d.SizeBytes.Value == 0
            ? "—"
            : $"{Math.Round((double)(d.SizeBytes.Value - d.FreeSpaceBytes.Value) / d.SizeBytes.Value * 100, 0):0}%";
    }

    public string DeviceId { get; }
    public string Label { get; }
    public string Size { get; }
    public string Used { get; }
    public string Percent { get; }
}

/// <summary>One row in the GPU table.</summary>
public sealed class GpuItemViewModel
{
    public static GpuItemViewModel From(GpuInfo g) => new(g);

    private GpuItemViewModel(GpuInfo g)
    {
        Name = string.IsNullOrWhiteSpace(g.Name) ? "—" : g.Name.Trim();
        VideoMemory = g.AdapterRamBytes is null
            ? "—"
            : $"{Math.Round(g.AdapterRamBytes.Value / (1024d * 1024d * 1024d), 1):0.#} GB";
        Driver = string.IsNullOrWhiteSpace(g.DriverVersion) ? "—" : g.DriverVersion;
        Processor = string.IsNullOrWhiteSpace(g.VideoProcessor) ? "—" : g.VideoProcessor;
    }

    public string Name { get; }
    public string VideoMemory { get; }
    public string Driver { get; }
    public string Processor { get; }
}

/// <summary>One row in the network table.</summary>
public sealed class NetworkAdapterItemViewModel
{
    public static NetworkAdapterItemViewModel From(NetworkAdapter a) => new(a);

    private NetworkAdapterItemViewModel(NetworkAdapter a)
    {
        Name = string.IsNullOrWhiteSpace(a.Name) ? "—" : a.Name.Trim();
        Connection = string.IsNullOrWhiteSpace(a.NetConnectionId) ? "—" : a.NetConnectionId;
        Mac = string.IsNullOrWhiteSpace(a.MacAddress) ? "—" : a.MacAddress;
        Speed = a.SpeedBps is null ? "—" : FormatSpeed(a.SpeedBps.Value);
        IPv4 = string.IsNullOrWhiteSpace(a.IPv4Address) ? "—" : a.IPv4Address;
        Mask = string.IsNullOrWhiteSpace(a.SubnetMask) ? "—" : a.SubnetMask;
        Gateway = string.IsNullOrWhiteSpace(a.DefaultGateway) ? "—" : a.DefaultGateway;
    }

    public string Name { get; }
    public string Connection { get; }
    public string Mac { get; }
    public string Speed { get; }
    public string IPv4 { get; }
    public string Mask { get; }
    public string Gateway { get; }

    private static string FormatSpeed(ulong bps)
    {
        if (bps >= 1_000_000_000UL) return $"{bps / 1_000_000_000UL} Gbps";
        if (bps >= 1_000_000UL) return $"{bps / 1_000_000UL} Mbps";
        if (bps >= 1_000UL) return $"{bps / 1_000UL} Kbps";
        return $"{bps} bps";
    }
}
