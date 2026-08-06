using System.Management;
using ComputerRenameTool.Models;

namespace ComputerRenameTool.Services;

/// <summary>
/// Renames the local machine via WMI <c>Win32_ComputerSystem.Rename</c>.
///
/// Design history:
///   * v1 — <c>SetComputerNameExW</c> P/Invoke with <c>SE_RESTORE_NAME</c>.
///     Failed on the user's box: UAC elevation alone is insufficient on
///     Windows 10/11, the call needs a SYSTEM-context privilege that admin
///     user tokens don't carry, and <c>GetLastError() == 5</c>
///     (ERROR_ACCESS_DENIED) silently propagates from the kernel.
///   * v2 (this file) — use the WMI <c>Rename</c> method on
///     <c>Win32_ComputerSystem</c>. This is the same code path Microsoft's
///     "Rename this PC" settings page uses; it works under an admin user
///     token without <c>SE_RESTORE_NAME</c>. <c>ReturnValue == 0</c> means
///     the rename was accepted; non-zero codes are surfaced through
///     <see cref="RenameResult.HResult"/> using <c>0x8007XXXX</c> so the UI
///     message table stays consistent.
///
/// Failures never throw — they return a <see cref="RenameResult"/> carrying
/// the Win32 HRESULT so the view-model can keep rendering the UI.
/// </summary>
public sealed class ComputerRenameService : IComputerRenameService
{
    /// <summary>HRESULT for arbitrary WMI failure (no specific kernel code).</summary>
    private const int FacadeWin32Generic = unchecked((int)0x80070001);
    private const int E_InvalidArg = unchecked((int)0x80070057);

    public RenameResult Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return RenameResult.Failed(E_InvalidArg, "机器名不能为空。");
        }

        App.Logger?.Info($"Rename requested: '{newName}'");

        try
        {
            var result = InvokeWmiRename(newName);
            if (!result.IsSuccess)
            {
                App.Logger?.Warn($"Rename failed. HRESULT=0x{result.HResult:X8} {result.Message}");
            }
            return result;
        }
        catch (Exception ex)
        {
            App.Logger?.Error("Rename threw an unexpected exception.", ex);
            return RenameResult.Failed(unchecked((int)0x80000000) | ex.HResult, ex.Message);
        }
    }

    /// <summary>
    /// Perform the rename through WMI. We invoke <c>Rename</c> on the single
    /// <c>Win32_ComputerSystem</c> instance. WMI normalizes admin-token
    /// privilege differences internally, so this does not require the
    /// <c>SE_RESTORE_NAME</c> privilege that <c>SetComputerNameExW</c> does.
    /// </summary>
    private static RenameResult InvokeWmiRename(string newName)
    {
        ManagementObject? system = null;
        try
        {
            App.Logger?.Info("Connecting to WMI Win32_ComputerSystem...");
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                system = obj;
                break;
            }

            if (system is null)
            {
                App.Logger?.Error("Win32_ComputerSystem not found via WMI.");
                return RenameResult.Failed(FacadeWin32Generic, "未找到本机 WMI 计算机对象。");
            }

            App.Logger?.Info($"Invoking WMI Rename('{newName}') on '{system["Name"]}'");
            var invokeResult = system.InvokeMethod("Rename", new object[] { newName });
            // InvokeMethod returns the method's out-parameters; the WMI
            // provider for Win32_ComputerSystem.Rename surfaces the value
            // directly as a uint (Win32 error code; 0 == success).
            var returnValue = Convert.ToUInt32(invokeResult);
            App.Logger?.Info($"WMI Rename ReturnValue={returnValue} (0x{returnValue:X})");

            if (returnValue == 0)
            {
                App.Logger?.Info($"Computer renamed to '{newName}' via WMI.");
                return RenameResult.Success(newName);
            }

            var hresult = MapWmiReturnValue((int)returnValue);
            var message = RenameResult.MapHResultToMessage(hresult);
            App.Logger?.Error($"WMI Rename refused the new name. ReturnValue={returnValue} → HRESULT=0x{hresult:X8}");
            return RenameResult.Failed(hresult, message);
        }
        catch (ManagementException mex)
        {
            // WMI surface errors come back as ManagementException — most
            // commonly access-denied when the process is not admin, or
            // "Invalid parameter" if WMI itself is broken.
            App.Logger?.Error("WMI ManagementException.", mex);
            var status = (uint)mex.ErrorCode;
            var hresult = status != 0
                ? unchecked((int)0x80070000) | ((int)status & 0xFFFF)
                : FacadeWin32Generic;
            return RenameResult.Failed(
                hresult,
                RenameResult.MapHResultToMessage(hresult));
        }
        finally
        {
            system?.Dispose();
        }
    }

    /// <summary>
    /// Map <c>Win32_ComputerSystem.Rename</c> return values to HRESULT. The
    /// WMI provider returns Win32-style error codes in <c>ReturnValue</c>.
    /// See MSDN "Rename method of the Win32_ComputerSystem class" for the
    /// canonical list (0 = success, 1..N = Win32 errors).
    /// </summary>
    private static int MapWmiReturnValue(int wmiReturnValue)
    {
        if (wmiReturnValue == 0) return 0;

        // 1..15841 are Win32 error codes documented in winerror.h. Most
        // common ones encountered in practice: 5 (access denied), 1326
        // (logon failure), 1351 (ERROR_NO_SUCH_MEMBER), 87 (invalid
        // parameter), 53 (network path not found). Convert to the same
        // 0x8007XXXX HRESULT the rest of the app uses for consistency.
        return unchecked((int)0x80070000) | (wmiReturnValue & 0xFFFF);
    }
}
