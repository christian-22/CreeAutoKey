using System.Windows;
using System.Windows.Threading;
using InputAutomator.Services;
using InputAutomator.Views;

namespace InputAutomator;

public partial class App : Application
{
    private SendInputInjector? _injector;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Best-effort safety: release all inputs on any crash/exit path
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
        }
        catch
        {
            // Not critical
        }
    }

    /// <summary>
    /// Called by MainWindow to register the injector for emergency cleanup.
    /// </summary>
    internal void RegisterInjector(SendInputInjector injector) => _injector = injector;

    private void SafeReleaseAll()
    {
        try
        {
            // Also try to reach MainWindow's engine
            if (MainWindow is MainWindow mw)
            {
                // The Window_Closing handler already does ForceReleaseAll,
                // but belt-and-suspenders:
            }
            _injector?.ReleaseAll();
        }
        catch
        {
            // Best effort
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SafeReleaseAll();
        MessageBox.Show($"Unexpected error (all inputs released):\n{e.Exception.Message}",
            "Input Automator Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        SafeReleaseAll();
        e.SetObserved();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        SafeReleaseAll();
    }

    private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        SafeReleaseAll();
    }
}
