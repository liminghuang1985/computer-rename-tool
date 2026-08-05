using System.IO;
using System.Text.Json;

namespace ComputerRenameTool.Helpers;

/// <summary>
/// Persists a small "pending reboot" marker that the next launch of the tool
/// reads to remind the user. Uses a JSON file under <c>%LOCALAPPDATA%</c>
/// so we don't pollute the tool's own directory.
/// </summary>
public static class ToastNotifier
{
    private static readonly string StateDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "ComputerRenameTool");

    private static readonly string MarkerPath = Path.Combine(StateDirectory, "pending-reboot.json");

    /// <summary>
    /// Marker payload. <see cref="OldName"/> is preserved so the toast can
    /// say "your machine used to be X, now it will be Y".
    /// </summary>
    public sealed record PendingRebootMarker(string OldName, string NewName, DateTime CreatedAtUtc);

    /// <summary>Records a pending reboot. Overwrites any previous marker.</summary>
    public static void MarkPending(string oldName, string newName)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var marker = new PendingRebootMarker(oldName, newName, DateTime.UtcNow);
            File.WriteAllText(MarkerPath, JsonSerializer.Serialize(marker));
        }
        catch
        {
            // best effort — failure here just means the reminder is lost.
        }
    }

    /// <summary>Reads the pending marker, or <c>null</c> if none exists.</summary>
    public static PendingRebootMarker? ReadPending()
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return null;
            }
            return JsonSerializer.Deserialize<PendingRebootMarker>(File.ReadAllText(MarkerPath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Clears the marker (after the user has rebooted or dismissed the toast).</summary>
    public static void Clear()
    {
        try
        {
            if (File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
            }
        }
        catch
        {
            // ignored
        }
    }
}
