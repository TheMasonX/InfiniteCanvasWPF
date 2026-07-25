namespace InfiniteCanvas.Core;

public sealed class CoalescingAsyncAction : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Func<CancellationToken, Task> _action;
    private readonly Action<Exception>? _onActionFault;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _processingTask;
    private bool _requested;
    private bool _disposed;

    public CoalescingAsyncAction(
        Func<CancellationToken, Task> action,
        Action<Exception>? onActionFault = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
        _onActionFault = onActionFault;
    }

    public Task RequestAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requested = true;

            if (_processingTask is null || _processingTask.IsCompleted)
            {
                _processingTask = ProcessAsync();
            }

            return _processingTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? processingTask;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _requested = false;
            _lifetime.Cancel();
            processingTask = _processingTask;
        }

        if (processingTask is not null)
        {
            try
            {
                await processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
        }

        _lifetime.Dispose();
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            lock (_gate)
            {
                if (!_requested || _disposed)
                {
                    return;
                }

                _requested = false;
            }

            try
            {
                await _action(_lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReportActionFault(exception);
            }
        }
    }

    private void ReportActionFault(Exception exception)
    {
        try
        {
            _onActionFault?.Invoke(exception);
        }
        catch
        {
        }
    }
}