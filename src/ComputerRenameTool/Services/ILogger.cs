namespace ComputerRenameTool.Services;

/// <summary>
/// Structured logger contract. Implementations must be thread-safe and must
/// never throw — a logging failure must not crash the host.
/// </summary>
public interface ILogger
{
    /// <summary>Writes an informational entry.</summary>
    void Info(string message);

    /// <summary>Writes a warning entry, optionally including an exception.</summary>
    void Warn(string message, Exception? ex = null);

    /// <summary>Writes an error entry, optionally including an exception.</summary>
    void Error(string message, Exception? ex = null);
}
