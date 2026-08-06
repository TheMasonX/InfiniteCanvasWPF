using System.Windows;
using System.Windows.Threading;
using InfiniteCanvas.App.Logging;
using Serilog;

namespace InfiniteCanvas.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Logger = SerilogHost.Logger;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Log.Information("Application starting");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting");
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        SerilogHost.Shutdown();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on the WPF dispatcher");
        if (Current?.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ReportUnhandledException(e.Exception);
        }

        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled application exception; terminating: {IsTerminating}", e.IsTerminating);
            return;
        }

        Log.Fatal("Unhandled application error; terminating: {IsTerminating}. Details: {ExceptionObject}", e.IsTerminating, e.ExceptionObject);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
