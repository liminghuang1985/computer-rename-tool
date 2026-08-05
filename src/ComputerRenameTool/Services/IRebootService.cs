namespace ComputerRenameTool.Services;

/// <summary>
/// Schedules and cancels a deferred reboot via the built-in
/// <c>shutdown.exe</c> tool. The UI is responsible for displaying the
/// countdown — this service only orchestrates the OS-side schedule.
/// </summary>
public interface IRebootService
{
    /// <summary>
    /// Starts a reboot countdown (in seconds). When the countdown reaches
    /// zero, Windows will restart the machine.
    /// </summary>
    void InitiateReboot(int countdownSeconds = 60);

    /// <summary>Cancels a previously scheduled reboot.</summary>
    void CancelReboot();

    /// <summary>Raised when the OS reports that the reboot is imminent (a few seconds left).</summary>
    event EventHandler<RebootCountdownEventArgs>? CountdownTick;
}

/// <summary>Event payload carrying the seconds remaining.</summary>
public sealed class RebootCountdownEventArgs : EventArgs
{
    public int SecondsRemaining { get; }
    public RebootCountdownEventArgs(int secondsRemaining) => SecondsRemaining = secondsRemaining;
}
