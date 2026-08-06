namespace ComputerRenameTool.Models;

/// <summary>
/// Physical network adapter from <c>Win32_NetworkAdapter</c> with
/// <c>NetEnabled=TRUE</c>. <see cref="SpeedBps"/> is the link speed in bits
/// per second.
/// </summary>
public sealed record NetworkAdapter(
    string? Name,
    string? NetConnectionId,
    string? MacAddress,
    ulong? SpeedBps);
