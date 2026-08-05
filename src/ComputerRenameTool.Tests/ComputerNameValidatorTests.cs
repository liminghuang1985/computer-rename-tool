using ComputerRenameTool.Helpers;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Covers the input-validation rule set per DESIGN.md §10.1.
/// </summary>
public class ComputerNameValidatorTests
{
    [Theory]
    [InlineData("ABC")]
    [InlineData("ABC-123")]
    [InlineData("DESKTOP-ABC123")]
    [InlineData("a")]
    [InlineData("BJ-IT-001")]
    public void IsValid_AcceptsValidNames(string name)
    {
        var ok = ComputerNameValidator.IsValid(name, out var error);
        Assert.True(ok, $"expected valid, got error: {error}");
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("ABC DEF")]      // space
    [InlineData("ABC_DEF")]      // underscore
    [InlineData("机器")]          // Chinese
    [InlineData("A.B")]          // dot
    [InlineData("AB/CD")]        // slash
    [InlineData("AB\\CD")]       // backslash
    [InlineData("A!B")]          // special
    public void IsValid_RejectsInvalidCharacters(string name)
    {
        var ok = ComputerNameValidator.IsValid(name, out var error);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_RejectsEmpty()
    {
        var ok = ComputerNameValidator.IsValid("", out var error);
        Assert.False(ok);
        Assert.Contains("请输入", error);
    }

    [Fact]
    public void IsValid_RejectsNull()
    {
        var ok = ComputerNameValidator.IsValid(null, out var error);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_RejectsWhitespaceOnly()
    {
        var ok = ComputerNameValidator.IsValid("   ", out var error);
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void IsValid_RejectsOverLength()
    {
        var name = new string('A', ComputerNameValidator.MaxLength + 1);
        var ok = ComputerNameValidator.IsValid(name, out var error);
        Assert.False(ok);
        Assert.Contains("15", error);
    }

    [Fact]
    public void IsValid_AcceptsMaxLength()
    {
        var name = new string('A', ComputerNameValidator.MaxLength);
        var ok = ComputerNameValidator.IsValid(name, out _);
        Assert.True(ok);
    }
}
