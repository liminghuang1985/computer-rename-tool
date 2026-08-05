using System.Runtime.InteropServices;
using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Thin wrapper around <c>kernel32!SetComputerNameExW</c> (DESIGN.md §8). All
/// non-success outcomes are returned as <see cref="RenameResult"/>s — never
/// thrown — so the view-model can keep rendering the UI.
/// </summary>
public sealed class ComputerRenameService : IComputerRenameService
{
    /// <summary>
    /// Performs the rename. The kernel call requires the process to be
    /// elevated; failures surface as <see cref="RenameResult.HResult"/>-bearing
    /// result objects that the UI maps to user-friendly messages via
    /// <see cref="RenameResult.MapHResultToMessage"/>.
    /// </summary>
    public RenameResult Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return RenameResult.Failed(0x80070057, "机器名不能为空。");
        }

        try
        {
            var ok = SetComputerNameExW(ComputerNameFormat.ComputerNamePhysicalDnsHostname, newName);
            if (!ok)
            {
                var err = Marshal.GetLastWin32Error();
                return RenameResult.Failed(ToHResult(err), RenameResult.MapHResultToMessage(ToHResult(err)));
            }

            App.Logger?.Info($"Computer renamed to '{newName}'.");
            return RenameResult.Success(newName);
        }
        catch (Exception ex)
        {
            App.Logger?.Error("SetComputerNameExW threw.", ex);
            return RenameResult.Failed(unchecked((int)0x80000000) | ex.HResult, ex.Message);
        }
    }

    /// <summary>
    /// Convert a raw Win32 error code (as returned by <c>GetLastError</c>) into
    /// an HRESULT of the form <c>0x8007XXXX</c>, matching the format used by
    /// the rest of the codebase and the exception-mapping table.
    /// </summary>
    private static int ToHResult(int win32Error) => unchecked((int)0x80070000) | (win32Error & 0xFFFF);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetComputerNameExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetComputerNameExW(ComputerNameFormat nameType, string lpBuffer);

    private enum ComputerNameFormat
    {
        ComputerNamePhysicalNetBIOS = 5,
        ComputerNamePhysicalDnsHostname = 6,
    }
}
