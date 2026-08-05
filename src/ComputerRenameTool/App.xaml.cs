using System.Windows;
using System.Windows.Threading;

namespace ComputerRenameTool;

/// <summary>
/// Application entry point. Handles unhandled exceptions and wires up the global
/// logger so startup crashes still produce a trace file under Logs/.
/// </summary>
public partial class App : Application
{
    public static Services.ILogger Logger { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger = new Services.FileLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Logger.Info("Application starting.");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show(
            $"发生未预期的错误:\n{e.Exception.Message}",
            "Computer Rename Tool",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Error("Unhandled domain exception.", ex);
        }
    }
}
