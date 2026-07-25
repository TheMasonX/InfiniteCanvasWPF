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

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.EventLog("InfiniteCanvas", manageEventSource: true, restrictedToMinimumLevel: LogEventLevel.Warning)
            .CreateLogger();

        return configuration;
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
