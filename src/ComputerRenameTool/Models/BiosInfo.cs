namespace ComputerRenameTool.Models;

/// <summary>
/// BIOS descriptor from <c>Win32_BIOS</c>.
/// </summary>
public sealed record BiosInfo(
    string? Manufacturer,
    string? SmbiosVersion,
    string? ReleaseDate);
