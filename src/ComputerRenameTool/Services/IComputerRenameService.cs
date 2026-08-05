using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Issues a computer-name change through the Win32 <c>SetComputerNameEx</c>
/// API. Does NOT trigger a reboot — the reboot is the reboot service's job
/// (DESIGN.md §4.2).
/// </summary>
public interface IComputerRenameService
{
    /// <summary>
    /// Renames the machine.
    /// </summary>
    /// <param name="newName">Already-validated new name (caller-side rule check).</param>
    RenameResult Rename(string newName);
}
