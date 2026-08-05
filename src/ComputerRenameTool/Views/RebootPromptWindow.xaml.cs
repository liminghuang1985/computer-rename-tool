using System.Diagnostics;
using System.Windows;

namespace ComputerRenameTool.Views;

/// <summary>
/// Modal countdown window. The cancel button aborts the scheduled reboot by
/// re-running the same shutdown command with a cancel flag (the
/// <see cref="ComputerRenameTool.Services.IRebootService"/> handles this
/// internally when the window closes).
/// </summary>
public partial class RebootPromptWindow : Window
{
    public RebootPromptWindow()
    {
        InitializeComponent();
    }

    /// <summary>Updates the countdown display. Safe to call from any thread.</summary>
    public void UpdateCountdown(int secondsRemaining)
    {
        CountdownText.Text = secondsRemaining.ToString();
        if (secondsRemaining <= 10)
        {
            CountdownText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // The owner MainWindow listens for Closing and will cancel the
        // scheduled reboot via RebootService.CancelReboot.
        Close();
    }

    private void RebootNowButton_Click(object sender, RoutedEventArgs e)
    {
        // Run shutdown.exe -r -t 0 to skip the remainder of the countdown.
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "-r -t 0",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            App.Logger?.Error("Manual reboot failed.", ex);
            MessageBox.Show(this,
                ComputerRenameTool.Models.RenameResult.MapHResultToMessage(0x80070015),
                "启动重启失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Close();
    }
}
