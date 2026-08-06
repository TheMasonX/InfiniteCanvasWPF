using Serilog;

namespace InfiniteCanvas.App;

internal static class SafeAsyncEventHandler
{
    public static async void Handle(
        Func<Task> handler,
        Action<Exception>? reportError = null,
        string operation = "async event handler")
    {
        ArgumentNullException.ThrowIfNull(handler);

        try
        {
            await handler().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Canceled {Operation}", operation);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed {Operation}", operation);
            reportError?.Invoke(exception);
        }
    }
}
