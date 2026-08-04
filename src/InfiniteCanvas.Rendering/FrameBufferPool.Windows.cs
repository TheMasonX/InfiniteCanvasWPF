#if WINDOWS
namespace InfiniteCanvas.Rendering;

/// <summary>
/// Owns the rotating frame buffers for the zero-copy render pipeline.
/// Front is displayed, back is rendered, and buffers that leave the screen
/// become reusable only after WPF's compositor has advanced past them.
/// </summary>
/// <remarks>
/// WPF's composition thread reads the InteropBitmap backing section
/// asynchronously after a frame is published. A buffer must not be rewritten
/// until the compositor has finished the frame that displayed it. The pool
/// therefore holds a retired buffer for two composition passes
/// (<see cref="OnCompositionFrame"/>) before it becomes reusable. Rewriting a
/// buffer too early showed the compositor a cleared or partially drawn
/// section, which appeared as black horizontal bands during fast scrolling
/// (ICW-P0-BUFFER-REUSE-SYNC, ICW-318).
///
/// All members must be called on the WPF UI thread. The render loop and the
/// composition callback both run on that thread.
/// </remarks>
public sealed class FrameBufferPool : IDisposable
{
    private readonly Queue<ZeroCopyBitmapFactory> _retiring = new();
    private readonly Queue<ZeroCopyBitmapFactory> _confirmed = new();
    private readonly Queue<ZeroCopyBitmapFactory> _reusable = new();
    private ZeroCopyBitmapFactory? _front;
    private ZeroCopyBitmapFactory? _back;
    private bool _disposed;

    /// <summary>Gets the buffer currently presented to WPF.</summary>
    public ZeroCopyBitmapFactory? Front => _front;

    /// <summary>Gets the buffer the next frame renders into, if staged.</summary>
    public ZeroCopyBitmapFactory? Back => _back;

    /// <summary>Gets the number of buffers waiting for the compositor to advance.</summary>
    public int PendingCompositionCount => _retiring.Count + _confirmed.Count;

    /// <summary>Gets or creates the buffer the next frame renders into.</summary>
    public ZeroCopyBitmapFactory AcquireBackBuffer(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // A staged back buffer exists after a frame was discarded as stale.
        // Reuse it directly; it was never presented.
        if (_back is not null && _back.Width == width && _back.Height == height)
        {
            return _back;
        }

        // Reuse a buffer the compositor has confirmed it is done with.
        ZeroCopyBitmapFactory? candidate = null;
        while (_reusable.Count > 0)
        {
            var buffer = _reusable.Dequeue();
            if (buffer.Width == width && buffer.Height == height)
            {
                candidate = buffer;
                break;
            }

            // The buffer no longer matches the viewport size. Its composition
            // is done, so release it.
            buffer.Dispose();
        }

        if (candidate is not null)
        {
            _back?.Dispose();
            _back = candidate;
            return _back;
        }

        _back?.Dispose();
        _back = new ZeroCopyBitmapFactory(width, height);
        return _back;
    }

    /// <summary>Marks a rendered buffer as the presented front frame.</summary>
    public void Publish(ZeroCopyBitmapFactory renderedBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderedBuffer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_front is not null)
        {
            _retiring.Enqueue(_front);
        }

        _front = renderedBuffer;
        _back = null;
    }

    /// <summary>
    /// Advances the composition handoff. Call this once per WPF composition
    /// pass (for example from <see cref="System.Windows.Media.CompositionTarget.Rendering"/>).
    /// A retired buffer becomes reusable after two passes.
    /// </summary>
    public void OnCompositionFrame()
    {
        if (_disposed)
        {
            return;
        }

        // Buffers that survived one full pass are now safe to reuse.
        while (_confirmed.Count > 0)
        {
            _reusable.Enqueue(_confirmed.Dequeue());
        }

        // Buffers retired since the last pass start their one-pass wait.
        while (_retiring.Count > 0)
        {
            _confirmed.Enqueue(_retiring.Dequeue());
        }
    }

    /// <summary>Disposes all owned buffers.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _front?.Dispose();
        _back?.Dispose();
        DisposeQueue(_retiring);
        DisposeQueue(_confirmed);
        DisposeQueue(_reusable);
        _front = null;
        _back = null;
    }

    private static void DisposeQueue(Queue<ZeroCopyBitmapFactory> queue)
    {
        while (queue.Count > 0)
        {
            queue.Dequeue().Dispose();
        }
    }
}
#endif
