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
    /// elevated and to hold <c>SE_RESTORE_NAME</c>; failures surface as
    /// <see cref="RenameResult.HResult"/>-bearing result objects that the UI
    /// maps to user-friendly messages via
    /// <see cref="RenameResult.MapHResultToMessage"/>.
    /// </summary>
    public RenameResult Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return RenameResult.Failed(unchecked((int)0x80070057), "机器名不能为空。");
        }

        // SE_RESTORE_NAME must be enabled on the current process token before
        // SetComputerNameExW is called, otherwise the kernel returns
        // 0x80070005 (E_ACCESSDENIED) even when the user is a member of
        // Administrators and the binary manifest is requesting
        // requireAdministrator. This is the same adjustment the Windows
        // "System Properties" dialog performs internally.
        if (!EnablePrivilege(SE_RESTORE_NAME))
        {
            var err = Marshal.GetLastWin32Error();
            return RenameResult.Failed(ToHResult(err), RenameResult.MapHResultToMessage(ToHResult(err)));
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

    /// <summary>
    /// Enables the named privilege on the current process's primary token.
    /// Returns <c>true</c> if the privilege is now in the enabled state. If
    /// the privilege is not held by the user (e.g. non-admin) the call
    /// succeeds but the privilege remains disabled; callers should still
    /// proceed and let the Win32 call report the access-denied error so the
    /// user sees a consistent message.
    /// </summary>
    private static bool EnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken))
        {
            App.Logger?.Warn($"OpenProcessToken failed: {Marshal.GetLastWin32Error()}");
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                App.Logger?.Warn($"LookupPrivilegeValue('{privilegeName}') failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED,
                },
            };

            if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                App.Logger?.Warn($"AdjustTokenPrivileges failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // ERROR_NOT_ALL_ASSIGNED means the user does not hold the
            // privilege; that's a soft failure for our purposes (the
            // SetComputerNameExW call will produce a proper access-denied
            // HRESULT which the UI already maps).
            var lastErr = Marshal.GetLastWin32Error();
            if (lastErr == ERROR_NOT_ALL_ASSIGNED)
            {
                App.Logger?.Warn($"Privilege '{privilegeName}' not assigned to the user.");
                return false;
            }

            return true;
        }
        finally
        {
            CloseHandle(hToken);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetComputerNameExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetComputerNameExW(ComputerNameFormat nameType, string lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    private enum ComputerNameFormat
    {
        ComputerNamePhysicalNetBIOS = 5,
        ComputerNamePhysicalDnsHostname = 6,
    }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const int ERROR_NOT_ALL_ASSIGNED = 1300;
    private const string SE_RESTORE_NAME = "SeRestorePrivilege";

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }
}
