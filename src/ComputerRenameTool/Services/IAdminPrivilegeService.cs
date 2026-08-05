namespace ComputerRenameTool.Services;

/// <summary>
/// Detects the current integrity level and re-launches the executable elevated
/// via the UAC consent prompt.
/// </summary>
public interface IAdminPrivilegeService
{
    /// <summary>Returns <c>true</c> when the current process has admin rights.</summary>
    bool IsRunAsAdmin();

    /// <summary>
    /// Spawns a new instance of the current executable with the <c>runas</c>
    /// verb to trigger the UAC consent prompt.
    /// </summary>
    /// <returns><c>true</c> when the elevated process was successfully started.</returns>
    bool RestartAsAdmin();
}
