using System.Diagnostics;

namespace ComputerRenameTool.Services;

/// <summary>
/// Schedules a reboot by shelling out to <c>shutdown.exe</c>. Emits one
/// <see cref="IRebootService.CountdownTick"/> per second on a background
/// thread so the UI can render a live countdown without blocking.
/// </summary>
public sealed class RebootService : IRebootService, IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _runner;

    public event EventHandler<RebootCountdownEventArgs>? CountdownTick;

    public void InitiateReboot(int countdownSeconds = 60)
    {
        CancelReboot();

        var psi = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = $"-r -t {countdownSeconds} -c \"机器名已修改,系统将在 {countdownSeconds} 秒后重启。\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(psi);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _runner = Task.Run(() => RunCountdown(countdownSeconds, token), token);
    }

    public void CancelReboot()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_runner is not null)
        {
            try { _runner.Wait(TimeSpan.FromSeconds(1)); }
            catch { /* ignored */ }
            _runner = null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "-a",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi);
        }
        catch
        {
            // shutdown.exe returns non-zero when no reboot is scheduled —
            // that's expected after the system already rebooted.
        }
    }

    private async Task RunCountdown(int total, CancellationToken token)
    {
        for (var remaining = total; remaining > 0; remaining--)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            CountdownTick?.Invoke(this, new RebootCountdownEventArgs(remaining));
            try { await Task.Delay(TimeSpan.FromSeconds(1), token); }
            catch (TaskCanceledException) { return; }
        }
    }

    public void Dispose()
    {
        CancelReboot();
    }
}
