namespace ComputerRenameTool.Models;

/// <summary>
/// CPU descriptor sourced from <c>Win32_Processor</c>. All fields are nullable
/// so a driver failure surfaces as a missing value rather than a placeholder,
/// matching the "数据不可读" contract (FIX-REQUEST-7 §关键实现要求 1).
/// </summary>
public sealed record CpuInfo(
    string? Name,
    int? NumberOfCores,
    int? NumberOfLogicalProcessors,
    double? MaxClockSpeedGHz,
    ushort? LoadPercentage);
