using ComputerRenameTool.Services;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Regression coverage for the "info fields blank" bug (BUG-REPORT-2 §Bug A).
/// The service is expected to never throw, even on machines where registry
/// reads fail (e.g. stripped Windows installs). Individual fields fall back
/// to a placeholder string so the UI always renders something rather than an
/// empty cell.
/// </summary>
public class SystemInfoServiceTests
{
    [Fact]
    public void GetComputerInfo_DoesNotThrow()
    {
        var svc = new SystemInfoService();
        var ex = Record.Exception(() => svc.GetComputerInfo());
        Assert.Null(ex);
    }

    [Fact]
    public void GetHardwareInfo_DoesNotThrow()
    {
        var svc = new SystemInfoService();
        var ex = Record.Exception(() => svc.GetHardwareInfo());
        Assert.Null(ex);
    }

    [Fact]
    public void GetComputerInfo_ReturnsPopulatedFields()
    {
        var info = new SystemInfoService().GetComputerInfo();
        Assert.False(string.IsNullOrWhiteSpace(info.ComputerName),
            $"ComputerName was blank: '{info.ComputerName}'");
        Assert.False(string.IsNullOrWhiteSpace(info.WindowsVersion),
            $"WindowsVersion was blank: '{info.WindowsVersion}'");
        Assert.False(string.IsNullOrWhiteSpace(info.CurrentUser),
            $"CurrentUser was blank: '{info.CurrentUser}'");
    }

    [Fact]
    public void GetHardwareInfo_ReturnsKnownOrNull()
    {
        var info = new SystemInfoService().GetHardwareInfo();
        // Each field may be null (the VM substitutes "未知 (驱动未安装)" then),
        // but the call must complete without throwing.
        Assert.True(info.Cpu is null || !string.IsNullOrWhiteSpace(info.Cpu));
        Assert.True(info.Memory is null || !string.IsNullOrWhiteSpace(info.Memory));
        Assert.True(info.Gpu is null || !string.IsNullOrWhiteSpace(info.Gpu));
        Assert.True(info.Disk is null || !string.IsNullOrWhiteSpace(info.Disk));
    }

    [Fact]
    public void GetComputerInfo_ComputerNameMatchesEnvironment()
    {
        var info = new SystemInfoService().GetComputerInfo();
        Assert.Equal(Environment.MachineName, info.ComputerName);
    }

    [Fact]
    public void GetComputerInfo_CurrentUserMatchesEnvironment()
    {
        var info = new SystemInfoService().GetComputerInfo();
        Assert.Equal(Environment.UserName, info.CurrentUser);
    }
}