using System.IO;
using System.Text;

namespace ComputerRenameTool.Services;

/// <summary>
/// Writes log entries to <c>Logs/rename-tool-YYYY-MM-DD.log</c> next to the
/// executable. Rotates by day and deletes log files older than
/// <see cref="RetentionDays"/>. All disk I/O failures are swallowed — a logging
/// failure must not crash the rename flow.
/// </summary>
public sealed class FileLogger : ILogger
{
    private const int RetentionDays = 30;
    private static readonly string LogDirectory =
        Path.Combine(AppContext.BaseDirectory, "Logs");

    private readonly object _gate = new();

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_gate)
            {
                EnsureDirectory();
                CleanupOldLogs();

                var fileName = $"rename-tool-{DateTime.Now:yyyy-MM-dd}.log";
                var path = Path.Combine(LogDirectory, fileName);

                var sb = new StringBuilder();
                sb.Append('=', 60).AppendLine();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
                sb.Append(level).Append(": ").AppendLine(message);
                if (ex is not null)
                {
                    sb.AppendLine(ex.ToString());
                }

                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "rename-tool-*.log"))
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTime < cutoff)
                {
                    fi.Delete();
                }
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
