using System.Windows;

namespace ComputerRenameTool.Helpers;

/// <summary>
/// Thin wrapper over <see cref="Clipboard"/>. Centralized so the
/// STA-thread dance lives in one place — WPF throws
/// <see cref="COMException"/> when called from a non-UI thread.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// Copies <paramref name="text"/> to the system clipboard. Returns
    /// <c>true</c> on success.
    /// </summary>
    public static bool CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            // Clipboard occasionally locks up under other apps; safe to swallow.
            return false;
        }
    }
}
