using ComputerRenameTool.Models;
using ComputerRenameTool.Services;
using ComputerRenameTool.ViewModels;
using Xunit;

namespace ComputerRenameTool.Tests;

/// <summary>
/// Behavioural tests for the rename state machine (DESIGN.md §5.3). Uses a
/// fake <see cref="IComputerRenameService"/> so the VM can be exercised
/// without touching the OS rename API.
/// </summary>
public class RenameViewModelTests
{
    private sealed class FakeRenameService : IComputerRenameService
    {
        public string? LastName { get; private set; }
        public bool FailWithAccessDenied { get; set; }
        public RenameResult Rename(string newName)
        {
            LastName = newName;
            return FailWithAccessDenied
                ? RenameResult.Failed(0x80070005, "denied")
                : RenameResult.Success(newName);
        }
    }

    [Fact]
    public void EmptyInput_DisablesSubmit()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME");
        Assert.Equal(ValidationState.Empty, vm.State);
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public void InvalidInput_DisablesSubmit()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME")
        {
            InputName = "bad name"
        };
        Assert.Equal(ValidationState.Invalid, vm.State);
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public void TooLongInput_DisablesSubmit()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME")
        {
            InputName = new string('A', 16)
        };
        Assert.Equal(ValidationState.TooLong, vm.State);
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public void SameAsCurrent_DisablesSubmitWithMessage()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME")
        {
            InputName = "OLD-NAME"
        };
        Assert.Equal(ValidationState.SameAsCurrent, vm.State);
        Assert.False(vm.CanSubmit);
        Assert.Contains("未变化", vm.ValidationMessage);
    }

    [Fact]
    public void ValidInput_EnablesSubmit()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME")
        {
            InputName = "NEW-NAME"
        };
        Assert.Equal(ValidationState.Valid, vm.State);
        Assert.True(vm.CanSubmit);
        Assert.Equal(string.Empty, vm.ValidationMessage);
    }

    [Fact]
    public void UseSuggested_PopulatesInput()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME", "SUGGEST-01");
        Assert.True(vm.HasSuggestion);
        Assert.True(vm.UseSuggestedCommand.CanExecute(null));
        vm.UseSuggestedCommand.Execute(null);
        Assert.Equal("SUGGEST-01", vm.InputName);
    }

    [Fact]
    public void UseSuggested_NotAvailableWhenNoSuggestion()
    {
        var vm = new RenameViewModel(new FakeRenameService(), "OLD-NAME");
        Assert.False(vm.HasSuggestion);
        Assert.False(vm.UseSuggestedCommand.CanExecute(null));
    }
}
