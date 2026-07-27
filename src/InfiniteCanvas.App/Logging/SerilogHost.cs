using Serilog;
using Serilog.Events;

namespace InfiniteCanvas.App.Logging;

internal static class SerilogHost
{
    private static ILogger? _logger;

    public static ILogger Logger => _logger ??= CreateLogger();

    private static ILogger CreateLogger()
    {
        var logDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InfiniteCanvas",
            "logs");
        System.IO.Directory.CreateDirectory(logDirectory);

        var filePath = System.IO.Path.Combine(logDirectory, "infinitecanvas-.log");
        string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Trace(outputTemplate: outputTemplate)
            .WriteTo.File(
                path: filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: outputTemplate);

        try
        {
            configuration.WriteTo.EventLog("InfiniteCanvas", manageEventSource: true, restrictedToMinimumLevel: LogEventLevel.Warning);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Falling back to file-only logging because the EventLog sink could not be initialized: {exception.Message}");
        }

        return configuration.CreateLogger();
    }

    public static void Shutdown()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            // ignore
        }
    }
}

