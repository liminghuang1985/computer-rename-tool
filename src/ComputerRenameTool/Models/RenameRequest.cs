namespace ComputerRenameTool.Models;

/// <summary>
/// Immutable request describing a rename operation. Captured at submit time so
/// the service layer is decoupled from view-model timing concerns.
/// </summary>
/// <param name="CurrentName">Machine name before the call.</param>
/// <param name="NewName">Desired machine name (already validated by caller).</param>
public sealed record RenameRequest(string CurrentName, string NewName);
