using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Tests the input-validation edge of <see cref="ComputerRenameService"/>.
/// The WMI path itself can only be exercised on a real Windows machine
/// with a working <c>Win32_ComputerSystem</c> provider and an admin token,
/// so we assert only on the cheap early-return behavior here.
/// </summary>
public class ComputerRenameServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyInput_FailsWithoutCallingWmi(string? newName)
    {
        var result = new ComputerRenameService().Rename(newName!);
        Assert.False(result.IsSuccess);
        Assert.Contains("机器名", result.Message);
    }
}
