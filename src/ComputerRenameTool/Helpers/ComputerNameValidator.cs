using System.Text.RegularExpressions;

namespace ComputerRenameTool.Helpers;

/// <summary>
/// Static validator that enforces the Windows computer-name ruleset
/// (PRD §五 / DESIGN.md §7). Returns the user-facing message via an
/// <c>out</c> parameter so callers don't have to re-derive it.
/// </summary>
public static class ComputerNameValidator
{
    /// <summary>Maximum allowed machine-name length (Windows NetBIOS limit).</summary>
    public const int MaxLength = 15;

    /// <summary>Allowed characters: A-Z, a-z, 0-9, hyphen.</summary>
    private static readonly Regex ValidPattern =
        new(@"^[A-Za-z0-9\-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> is a valid computer
    /// name. When false, <paramref name="error"/> contains the user-facing
    /// reason; when true, it is the empty string.
    /// </summary>
    public static bool IsValid(string? name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "请输入新的机器名";
            return false;
        }

        if (name.Length > MaxLength)
        {
            error = $"机器名长度不能超过{MaxLength}个字符";
            return false;
        }

        if (!ValidPattern.IsMatch(name))
        {
            error = "机器名只能包含字母、数字和 \"-\"";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Same as <see cref="IsValid"/> but ignores the empty case — useful when
    /// the caller wants a dedicated "Empty" state instead of treating empty
    /// as "Invalid".
    /// </summary>
    public static bool IsFormatValid(string? name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "请输入新的机器名";
            return false;
        }
        return IsValid(name, out error);
    }
}
