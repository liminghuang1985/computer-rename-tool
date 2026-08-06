using ComputerRenameTool.Models;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Round-trip / sanity tests for the new hardware model records. Each record
/// is immutable so this is mostly a smoke test that the property names and
/// types match what the WMI adapter produces. (FIX-REQUEST-7 §单元测试)
/// </summary>
public class HardwareModelTests
{
    [Fact]
    public void MemoryChip_FieldsRoundTrip()
    {
        var c = new MemoryChip("DIMM A1", "Samsung", "M471A2K43", 17179869184UL, 4800, 13, 1);
        Assert.Equal("DIMM A1", c.DeviceLocator);
        Assert.Equal("Samsung", c.Manufacturer);
        Assert.Equal("M471A2K43", c.PartNumber);
        Assert.Equal(17179869184UL, c.CapacityBytes);
        Assert.Equal(4800u, c.SpeedMHz);
        Assert.Equal((ushort)13, c.FormFactor);
        Assert.Equal(1, c.SlotNumber);
    }

    [Fact]
    public void PhysicalDisk_FieldsRoundTrip()
    {
        var d = new PhysicalDisk("Samsung 990 PRO", 1024UL * 1024 * 1024 * 1024, "NVMe", "OK", "S123");
        Assert.Equal("Samsung 990 PRO", d.Model);
        Assert.Equal("NVMe", d.InterfaceType);
        Assert.Equal("OK", d.Status);
        Assert.Equal("S123", d.SerialNumber);
    }

    [Fact]
    public void LogicalDisk_FieldsRoundTrip()
    {
        var d = new LogicalDisk("C:", "System", 500UL * 1024 * 1024 * 1024, 200UL * 1024 * 1024 * 1024);
        Assert.Equal("C:", d.DeviceId);
        Assert.Equal("System", d.VolumeName);
        Assert.Equal(500UL * 1024 * 1024 * 1024, d.SizeBytes);
        Assert.Equal(200UL * 1024 * 1024 * 1024, d.FreeSpaceBytes);
    }

    [Fact]
    public void GpuInfo_FieldsRoundTrip()
    {
        var g = new GpuInfo("NVIDIA RTX 4060", "31.0.15.4601", 8UL * 1024 * 1024 * 1024, "AD107");
        Assert.Equal("NVIDIA RTX 4060", g.Name);
        Assert.Equal("31.0.15.4601", g.DriverVersion);
        Assert.Equal("AD107", g.VideoProcessor);
    }

    [Fact]
    public void NetworkAdapter_FieldsRoundTrip()
    {
        var a = new NetworkAdapter("Wi-Fi", "WiFi", "aa:bb:cc:dd:ee:ff", 1_000_000_000UL, "10.12.138.38", "255.255.255.0", "10.12.138.1");
        Assert.Equal("Wi-Fi", a.Name);
        Assert.Equal("WiFi", a.NetConnectionId);
        Assert.Equal("aa:bb:cc:dd:ee:ff", a.MacAddress);
        Assert.Equal(1_000_000_000UL, a.SpeedBps);
        Assert.Equal("10.12.138.38", a.IPv4Address);
        Assert.Equal("255.255.255.0", a.SubnetMask);
        Assert.Equal("10.12.138.1", a.DefaultGateway);
    }

    [Fact]
    public void BiosInfo_FieldsRoundTrip()
    {
        var b = new BiosInfo("Dell", "1.5", "20240115");
        Assert.Equal("Dell", b.Manufacturer);
        Assert.Equal("1.5", b.SmbiosVersion);
        Assert.Equal("20240115", b.ReleaseDate);
    }

    [Fact]
    public void MotherboardInfo_FieldsRoundTrip()
    {
        var m = new MotherboardInfo("Dell Inc.", "Latitude 5540", "ABC123");
        Assert.Equal("Dell Inc.", m.Manufacturer);
        Assert.Equal("Latitude 5540", m.Product);
        Assert.Equal("ABC123", m.SerialNumber);
    }

    [Fact]
    public void OperatingSystemInfo_FieldsRoundTrip()
    {
        var o = new OperatingSystemInfo("Windows 11 Pro", "10.0", "22631", "20240101000000.000000+000", "20260804090012.123456+000", "OS-SN", 34359738368UL, 17179869184UL);
        Assert.Equal("Windows 11 Pro", o.Caption);
        Assert.Equal("22631", o.BuildNumber);
        Assert.Equal("OS-SN", o.SerialNumber);
    }

    [Fact]
    public void CpuInfo_FieldsRoundTrip()
    {
        var c = new CpuInfo("Intel i7-14700", 20, 28, 5.4, (ushort)23);
        Assert.Equal("Intel i7-14700", c.Name);
        Assert.Equal(20, c.NumberOfCores);
        Assert.Equal(28, c.NumberOfLogicalProcessors);
        Assert.Equal(5.4, c.MaxClockSpeedGHz);
        Assert.Equal((ushort)23, c.LoadPercentage);
    }

    [Fact]
    public void HardwareReport_Empty_FillsEmptyCollections()
    {
        var r = HardwareReport.Empty(new ComputerInfo("X", "Y", "Z"));
        Assert.Equal("X", r.Computer.ComputerName);
        Assert.Null(r.Cpu);
        Assert.Null(r.OperatingSystem);
        Assert.Null(r.Bios);
        Assert.Null(r.Motherboard);
        Assert.Empty(r.MemoryChips);
        Assert.Empty(r.PhysicalDisks);
        Assert.Empty(r.LogicalDisks);
        Assert.Empty(r.Gpus);
        Assert.Empty(r.NetworkAdapters);
    }
}
