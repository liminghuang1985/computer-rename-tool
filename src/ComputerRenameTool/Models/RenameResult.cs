namespace ComputerRenameTool.Models;

/// <summary>
/// Result of a rename attempt. Errors carry the Win32 HRESULT (as an
/// unsigned 32-bit value) so the UI can render the exact error code from
/// DESIGN.md §13.4 / PRD §八 exception mapping.
/// </summary>
public sealed record RenameResult(bool IsSuccess, string NewName, int HResult, string Message)
{
    public static RenameResult Success(string newName) =>
        new(true, newName, 0, "修改成功");

    public static RenameResult Failed(int hResult, string message) =>
        new(false, string.Empty, hResult, message);

    /// <summary>
    /// Maps a Win32 HRESULT to the user-facing message required by the
    /// exception-mapping table (PRD §十三.4 / DESIGN.md §8).
    /// </summary>
    public static string MapHResultToMessage(int hResult) => hResult switch
    {
        unchecked((int)0x80070005) => "修改失败,请确认具有管理员权限。",
        unchecked((int)0x8007007B) => "机器名格式错误",
        unchecked((int)0x8007089A) => "当前网络环境不允许修改机器名",
        unchecked((int)0x80070015) => "启动重启失败,请手动重启",
        _ => $"系统调用失败 (HRESULT 0x{hResult:X8})"
    };
}
