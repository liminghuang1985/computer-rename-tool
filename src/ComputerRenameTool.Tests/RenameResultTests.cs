using ComputerRenameTool.Models;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Tests the HRESULT → user-message mapping (DESIGN.md §13.4 / PRD §十三.4).
/// </summary>
public class RenameResultTests
{
    [Theory]
    [InlineData(0x80070005, "管理员")]    // access denied
    [InlineData(0x8007007B, "格式")]      // invalid name
    [InlineData(0x8007089A, "网络")]      // network
    [InlineData(0x80070015, "重启")]      // reboot failure
    public void MapHResultToMessage_KnownCodes(int hresult, string mustContain)
    {
        var msg = RenameResult.MapHResultToMessage(hresult);
        Assert.Contains(mustContain, msg);
    }

    [Fact]
    public void MapHResultToMessage_UnknownCodeReturnsHex()
    {
        var msg = RenameResult.MapHResultToMessage(0xDEADBEEF);
        Assert.Contains("0xDEADBEEF", msg);
    }

    [Fact]
    public void Success_HasNoError()
    {
        var r = RenameResult.Success("BJ-IT-001");
        Assert.True(r.IsSuccess);
        Assert.Equal(0, r.HResult);
        Assert.Equal("BJ-IT-001", r.NewName);
    }

    [Fact]
    public void Failed_PreservesHResult()
    {
        var r = RenameResult.Failed(0x80070005, "denied");
        Assert.False(r.IsSuccess);
        Assert.Equal(0x80070005, r.HResult);
        Assert.Equal("denied", r.Message);
    }
}
