using System.Threading;

namespace InfiniteCanvas.Core;

public sealed class CameraTransform
{
    // LUKE: I SET THESE EXTREMELY WIDE ON PURPOSE. DO NOT TOUCH.
    // The minimum scale is effectively a zoom-out limit, and the maximum scale is effectively a zoom-in limit.
    // The actual zoom-in/out limits are determined by the viewport size and the content bounds, which are enforced in `ClampToBounds`.
    private const double MinimumScale = 0.0000000001;
    private const double MaximumScale = 10000;

    private readonly double _minimumScale;
    private readonly double _maximumScale;
    private TransformState _state = TransformState.Identity;

    public CameraTransform(double minimumScale = MinimumScale, double maximumScale = MaximumScale)
    {
        if (!double.IsFinite(minimumScale) || minimumScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScale));
        }

        if (!double.IsFinite(maximumScale) || maximumScale < minimumScale)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumScale));
        }

        _minimumScale = minimumScale;
        _maximumScale = maximumScale;
    }

    public double ScaleX => Volatile.Read(ref _state).ScaleX;

    public double ScaleY => Volatile.Read(ref _state).ScaleY;

    public CameraSnapshot Capture()
    {
        var state = Volatile.Read(ref _state);
        return new CameraSnapshot(state.ScaleX, state.ScaleY, state.OffsetX, state.OffsetY);
    }

    public void Pan(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        }

        if (!double.IsFinite(deltaY))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaY));
        }

        Update(state => state with
        {
            OffsetX = state.OffsetX + deltaX,
            OffsetY = state.OffsetY + deltaY
        });
    }

    public bool Zoom(double scaleDelta, ScreenPoint origin)
    {
        return Zoom(scaleDelta, scaleDelta, origin);
    }

    public bool Zoom(double scaleXDelta, double scaleYDelta, ScreenPoint origin)
    {
        if (!IsPositiveFinite(scaleXDelta)
            || !IsPositiveFinite(scaleYDelta)
            || !double.IsFinite(origin.X)
            || !double.IsFinite(origin.Y))
        {
            return false;
        }

        while (true)
        {
            var current = Volatile.Read(ref _state);
            var nextScaleX = current.ScaleX * scaleXDelta;
            var nextScaleY = current.ScaleY * scaleYDelta;

            if (!IsScaleAllowed(nextScaleX) || !IsScaleAllowed(nextScaleY))
            {
                return false;
            }

            var next = new TransformState(
                nextScaleX,
                nextScaleY,
                origin.X + ((current.OffsetX - origin.X) * scaleXDelta),
                origin.Y + ((current.OffsetY - origin.Y) * scaleYDelta));

            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, next, current), current))
            {
                return true;
            }
        }
    }

    public ScreenPoint WorldToScreen(double worldX, double worldY)
    {
        var state = Volatile.Read(ref _state);
        return new ScreenPoint(
            (worldX * state.ScaleX) + state.OffsetX,
            (worldY * state.ScaleY) + state.OffsetY);
    }

    public SpatialBounds GetViewportBounds(double screenWidth, double screenHeight)
    {
        if (!double.IsFinite(screenWidth) || screenWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenWidth));
        }

        if (!double.IsFinite(screenHeight) || screenHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenHeight));
        }

        var state = Volatile.Read(ref _state);
        var left = -state.OffsetX / state.ScaleX;
        var top = -state.OffsetY / state.ScaleY;

        return new SpatialBounds(
            left,
            top,
            screenWidth / state.ScaleX,
            screenHeight / state.ScaleY);
    }

    public void ClampToBounds(SpatialBounds bounds, double screenWidth, double screenHeight)
    {
        if (!double.IsFinite(screenWidth) || screenWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenWidth));
        }

        if (!double.IsFinite(screenHeight) || screenHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenHeight));
        }

        Update(state => state with
        {
            OffsetX = ClampOffset(state.ScaleX, state.OffsetX, bounds.X, bounds.Right, screenWidth),
            OffsetY = ClampOffset(state.ScaleY, state.OffsetY, bounds.Y, bounds.Bottom, screenHeight)
        });
    }

    private void Update(Func<TransformState, TransformState> update)
    {
        while (true)
        {
            var current = Volatile.Read(ref _state);
            var next = update(current);

            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, next, current), current))
            {
                return;
            }
        }
    }

    private bool IsScaleAllowed(double scale)
    {
        return double.IsFinite(scale) && scale >= _minimumScale && scale <= _maximumScale;
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0;
    }

    private static double ClampOffset(
        double scale,
        double offset,
        double worldStart,
        double worldEnd,
        double screenLength)
    {
        var contentLength = (worldEnd - worldStart) * scale;
        if (contentLength <= screenLength)
        {
            return ((screenLength - contentLength) / 2) - (worldStart * scale);
        }

        var minimum = screenLength - (worldEnd * scale);
        var maximum = -(worldStart * scale);
        return Math.Clamp(offset, minimum, maximum);
    }

    private sealed record TransformState(double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        public static TransformState Identity { get; } = new(1, 1, 0, 0);
    }
}

public readonly record struct CameraSnapshot(
    double ScaleX,
    double ScaleY,
    double OffsetX,
    double OffsetY)
{
    public ScreenPoint WorldToScreen(double worldX, double worldY)
    {
        return new ScreenPoint(
            (worldX * ScaleX) + OffsetX,
            (worldY * ScaleY) + OffsetY);
    }

    public SpatialBounds GetViewportBounds(double screenWidth, double screenHeight)
    {
        if (!double.IsFinite(screenWidth) || screenWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenWidth));
        }

        if (!double.IsFinite(screenHeight) || screenHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenHeight));
        }

        return new SpatialBounds(
            -OffsetX / ScaleX,
            -OffsetY / ScaleY,
            screenWidth / ScaleX,
            screenHeight / ScaleY);
    }
}
