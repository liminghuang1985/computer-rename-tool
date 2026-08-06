namespace ComputerRenameTool.Models;

/// <summary>
/// Base board (motherboard) descriptor from <c>Win32_BaseBoard</c>.
/// </summary>
public sealed record MotherboardInfo(
    string? Manufacturer,
    string? Product,
    string? SerialNumber);
