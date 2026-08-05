using System.Diagnostics;
using System.Security.Principal;

namespace ComputerRenameTool.Services;

/// <summary>
/// Windows-integrity-level based admin check. The well-known SID
/// <c>S-1-5-32-544</c> is the BUILTIN\Administrators group; the caller's
/// primary token contains it only when the process is elevated.
/// </summary>
public sealed class AdminPrivilegeService : IAdminPrivilegeService
{
    private const string AdministratorsSid = "S-1-5-32-544";

    public bool IsRunAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator)
                || identity.Groups?.OfType<SecurityIdentifier>()
                       .Any(s => s.Value == AdministratorsSid) == true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestartAsAdmin()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName
                      ?? Environment.ProcessPath
                      ?? throw new InvalidOperationException("无法定位当前可执行文件。");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };

            return Process.Start(psi) is not null;
        }
        catch
        {
            return false;
        }
    }
}
