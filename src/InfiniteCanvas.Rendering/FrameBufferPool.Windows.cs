#if WINDOWS
namespace InfiniteCanvas.Rendering;

/// <summary>
/// Owns the rotating frame buffers for the zero-copy render pipeline.
/// Front is displayed, back is rendered, retired is the buffer that left
/// the screen one frame ago and is safe to recycle.
/// </summary>
/// <remarks>
/// WPF's composition thread reads the InteropBitmap backing section
/// asynchronously after a frame is published. Reusing a buffer immediately
/// after it leaves the screen can show the compositor a cleared or partially
/// drawn section, which appears as black flashes during fast scrolling.
/// The rotation gives the compositor one full frame cycle of slack before a
/// buffer is rewritten (ICW-P0-BUFFER-REUSE-SYNC).
/// </remarks>
public sealed class FrameBufferPool : IDisposable
{
    private ZeroCopyBitmapFactory? _front;
    private ZeroCopyBitmapFactory? _back;
    private ZeroCopyBitmapFactory? _retired;

    /// <summary>Gets the buffer currently presented to WPF.</summary>
    public ZeroCopyBitmapFactory? Front => _front;

    /// <summary>Gets the buffer the next frame renders into, if staged.</summary>
    public ZeroCopyBitmapFactory? Back => _back;

    /// <summary>Gets the buffer that left the screen one frame ago.</summary>
    public ZeroCopyBitmapFactory? Retired => _retired;

    /// <summary>Gets or creates the buffer the next frame renders into.</summary>
    public ZeroCopyBitmapFactory AcquireBackBuffer(int width, int height)
    {
        // A staged back buffer exists after a frame was discarded as stale.
        // Reuse it directly; it was never presented.
        if (_back is not null && _back.Width == width && _back.Height == height)
        {
            return _back;
        }

        // Recycle the retired buffer. It was presented two frames ago, so the
        // compositor has had a full frame cycle to finish reading it.
        if (_retired is not null && _retired.Width == width && _retired.Height == height)
        {
            _back?.Dispose();
            _back = _retired;
            _retired = null;
            return _back;
        }

        // No reusable buffer at this size. Release the non-presented slots and
        // allocate a fresh section.
        _back?.Dispose();
        _retired?.Dispose();
        _retired = null;
        _back = new ZeroCopyBitmapFactory(width, height);
        return _back;
    }

    /// <summary>Marks a rendered buffer as the presented front frame.</summary>
    public void Publish(ZeroCopyBitmapFactory renderedBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderedBuffer);

        // The retired slot holds a buffer that was presented two frames ago.
        // The compositor is done with it, so release it before rotating.
        _retired?.Dispose();

        _retired = _front;
        _front = renderedBuffer;
        _back = null;
    }

    /// <summary>Disposes all owned buffers.</summary>
    public void Dispose()
    {
        _front?.Dispose();
        _back?.Dispose();
        _retired?.Dispose();
        _front = null;
        _back = null;
        _retired = null;
    }
}
#endif
