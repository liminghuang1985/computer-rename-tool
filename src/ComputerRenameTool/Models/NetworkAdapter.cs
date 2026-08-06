namespace ComputerRenameTool.Models;

/// <summary>
/// Physical network adapter from <c>Win32_NetworkAdapter</c> with
/// <c>NetEnabled=TRUE</c>. <see cref="SpeedBps"/> is the link speed in bits
/// per second. IP fields come from <c>Win32_NetworkAdapterConfiguration</c>
/// joined on the adapter Index (FIX-REQUEST-8 — IP summary bug, was showing
/// NetConnectionID like "以太网 3" instead of the IPv4 address).
/// </summary>
public sealed record NetworkAdapter(
    string? Name,
    string? NetConnectionId,
    string? MacAddress,
    ulong? SpeedBps,
    string? IPv4Address,
    string? SubnetMask,
    string? DefaultGateway);
