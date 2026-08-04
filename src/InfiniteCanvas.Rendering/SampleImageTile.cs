using System.Diagnostics;
using InfiniteCanvas.Core;
using Serilog;
#if WINDOWS
using System.Drawing;
#endif

namespace InfiniteCanvas.Rendering;

public sealed class SampleImageTile
{
    private readonly Lock _cacheGate = new();
    private readonly Func<CancellationToken, byte[]> _pixelFactory;
    private readonly Func<int, CancellationToken, byte[]>? _mipPixelFactory;
    private readonly byte _placeholderValue;
    private readonly Dictionary<int, byte[]> _mipPixels = new();
    private readonly HashSet<int> _mipGenerationQueued = new();
    private byte[]? _pixels;
    private int _generationQueued;
    private int _generationEpoch;
    private long _generationDurationTicks;

    /// <summary>
    /// The current generation epoch for this tile. Incremented when
    /// <see cref="ResetImageCache"/> is called. Used by the viewport
    /// interest set to build cache keys that match the tile's current
    /// generation state.
    /// </summary>
    public int CurrentGenerationEpoch => Volatile.Read(ref _generationEpoch);
#if WINDOWS
    private int _backgroundFetched;
#endif
    private TileWorkCoordinator? _coordinator;
    private readonly object _perTileClaimant = new();

    /// <summary>
    /// Optional provider that returns the current frame's claimant ID for
    /// coordinator requests. When set, tile generation is attributed to the
    /// current frame, allowing per-frame viewport-aware cancellation.
    /// If null, a per-tile instance claimant is used (each tile has its own
    /// identity so RemoveAllClaimants only cancels that tile's work).
    /// </summary>
    public Func<object>? ClaimantIdProvider { get; set; }

    /// <summary>
    /// Optional provider that returns the cancellation token for the current
    /// frame or viewport. When set, this token is passed to the coordinator
    /// so that stale frames automatically remove their claimants.
    /// If null, CancellationToken.None is used (no auto-removal).
    /// </summary>
    public Func<CancellationToken>? ClaimantTokenProvider { get; set; }
    public Action<BackgroundTileCacheKey>? ReleaseReservedCacheEntry { get; set; }
    public event EventHandler? PixelsGenerated;
    public event EventHandler? PixelsGenerationFailed;

    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        Func<byte[]> pixelFactory,
        IReadOnlyList<SampleAnnotation> annotations,
        byte placeholderValue = 128,
        Func<int, byte[]>? mipPixelFactory = null,
        Func<CancellationToken, byte[]>? cancellablePixelFactory = null,
        Func<int, CancellationToken, byte[]>? cancellableMipPixelFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(pixelFactory);
        ArgumentNullException.ThrowIfNull(annotations);

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        Id = id;
        Bounds = bounds;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _placeholderValue = placeholderValue;
        _pixelFactory = cancellablePixelFactory is null
            ? _ => ValidatePixels(pixelFactory(), pixelWidth, pixelHeight)
            : token => ValidatePixels(cancellablePixelFactory(token), pixelWidth, pixelHeight);
        _mipPixelFactory = cancellableMipPixelFactory is not null
            ? (mipLevel, token) => ValidateMipPixels(cancellableMipPixelFactory(mipLevel, token), pixelWidth, pixelHeight, mipLevel)
            : mipPixelFactory is null
                ? null
                : (mipLevel, _) => ValidateMipPixels(mipPixelFactory(mipLevel), pixelWidth, pixelHeight, mipLevel);
        Annotations = annotations;
        // Optional shared defect template pool reference for lifecycle disposal.
        DefectTemplatePool = null;
    }

    internal IReadOnlyList<SampleImageGenerator.DefectTemplate>? DefectTemplatePool { get; set; }

    /// <summary>
    /// Dispose unique defect template pools from the given tile collection.
    /// The pool is shared across all tiles in a generation set, so we collect
    /// distinct references and dispose each pool exactly once.
    /// </summary>
    public static void DisposeDefectTemplatePools(IReadOnlyList<SampleImageTile> tiles)
    {
        if (tiles is null || tiles.Count == 0) return;

        var disposed = new HashSet<object>();
        for (var i = 0; i < tiles.Count; i++)
        {
            var pool = tiles[i].DefectTemplatePool;
            if (pool is not null && disposed.Add(pool))
            {
                DefectTemplateFactory.DisposePool(pool);
            }
        }
    }

    public string Id { get; }

    public SpatialBounds Bounds { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public byte PlaceholderValue => _placeholderValue;

    public long ResidentByteCount
    {
        get
        {
            lock (_cacheGate)
            {
                long total = _pixels?.LongLength ?? 0;
                foreach (var mip in _mipPixels.Values)
                {
                    total += mip.LongLength;
                }

                return total;
            }
        }
    }

    public bool IsImageGenerated => Volatile.Read(ref _pixels) is not null;

    public bool IsMipGenerated(int mipLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        lock (_cacheGate)
        {
            return mipLevel == 0 ? _pixels is not null : _mipPixels.ContainsKey(mipLevel);
        }
    }

    public bool IsGenerationQueued => Volatile.Read(ref _generationQueued) == 1 && !IsImageGenerated;

    public TimeSpan? GenerationDuration => IsImageGenerated
        ? DurationFromStopwatchTicks(Volatile.Read(ref _generationDurationTicks))
        : null;

#if WINDOWS
    public TimeSpan? BitmapGenerationDuration => null;

    public TimeSpan? BitmapConversionDuration => null;

    public bool IsBackgroundFetched => Volatile.Read(ref _backgroundFetched) == 1;
#else
    public bool IsBackgroundFetched => IsImageGenerated;
#endif

    public byte[] Pixels
    {
        get
        {
            var cached = Volatile.Read(ref _pixels);
            if (cached is not null)
            {
                return cached;
            }

            lock (_cacheGate)
            {
                if (_pixels is null)
                {
                    _pixels = _pixelFactory(CancellationToken.None);
#if WINDOWS
                    Interlocked.Exchange(ref _backgroundFetched, 1);
#endif
                    Interlocked.Exchange(ref _generationQueued, 1);
                }

                return _pixels;
            }
        }
    }

    public bool TryGetPixelsNonBlocking(
        out byte[] pixels,
        Func<BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry = null)
    {
        var cached = Volatile.Read(ref _pixels);
        if (cached is not null)
        {
            pixels = cached;
            return true;
        }

        // If no native pixels are available yet, start native generation and
        // attempt to return the best available mip as a fallback so the
        // renderer can display a lower-resolution image until the native
        // image finishes generating.
        EnsurePixelsGenerationStarted(tryReserveCacheEntry);

        if (_mipPixelFactory is null)
        {
            pixels = [];
            return false;
        }

        lock (_cacheGate)
        {
            var fallbackCandidates = new List<(int MipLevel, byte[] Pixels)>();
            if (_pixels is not null)
            {
                fallbackCandidates.Add((0, _pixels));
            }

            fallbackCandidates.AddRange(_mipPixels.Select(pair => (pair.Key, pair.Value)));

            var fallback = fallbackCandidates
                .OrderBy(candidate => Math.Abs(candidate.MipLevel - 0))
                .ThenBy(candidate => candidate.MipLevel)
                .FirstOrDefault(candidate => candidate.Pixels is not null);

            if (fallback.Pixels is not null)
            {
                pixels = fallback.Pixels;
                return true;
            }
        }

        pixels = [];
        return false;
    }

    public bool TryGetPixelsNonBlocking(
        int mipLevel,
        out byte[] pixels,
        Func<BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry = null)
    {
        return TryGetPixelsNonBlocking(mipLevel, out pixels, out _, tryReserveCacheEntry);
    }

    public bool TryGetPixelsNonBlocking(
        int mipLevel,
        out byte[] pixels,
        out int residentMipLevel,
        Func<BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);

        // For mip level 0, native pixels ARE the mip-0 data. Check them first
        // and trigger native generation (not mip-0 generation via the mip factory)
        // so the fallback logic below can correctly report residentMipLevel.
        if (mipLevel == 0)
        {
            if (TryGetNativePixels(out pixels!))
            {
                residentMipLevel = 0;
                return true;
            }

            EnsurePixelsGenerationStarted(tryReserveCacheEntry);
        }

        lock (_cacheGate)
        {
            if (_mipPixels.TryGetValue(mipLevel, out pixels!))
            {
                residentMipLevel = mipLevel;
                return true;
            }
        }

        if (_mipPixelFactory is null)
        {
            pixels = [];
            residentMipLevel = 0;
            return TryGetNativePixels(out pixels);
        }

        if (mipLevel > 0)
        {
            EnsureMipPixelsGenerationStarted(mipLevel, tryReserveCacheEntry);
        }

        lock (_cacheGate)
        {
            var fallbackCandidates = new List<(int MipLevel, byte[] Pixels)>();
            if (_pixels is not null)
            {
                fallbackCandidates.Add((0, _pixels));
            }

            fallbackCandidates.AddRange(_mipPixels.Select(pair => (pair.Key, pair.Value)));

            var fallback = fallbackCandidates
                .OrderBy(candidate => Math.Abs(candidate.MipLevel - mipLevel))
                .ThenBy(candidate => candidate.MipLevel)
                .FirstOrDefault(candidate => candidate.Pixels is not null);
            if (fallback.Pixels is not null)
            {
                pixels = fallback.Pixels;
                residentMipLevel = fallback.MipLevel;
                return true;
            }
        }

        pixels = [];
        residentMipLevel = 0;
        return false;
    }

    private bool TryGetNativePixels(out byte[] pixels)
    {
        var cached = Volatile.Read(ref _pixels);
        if (cached is not null)
        {
            pixels = cached;
            return true;
        }

        pixels = [];
        return false;
    }

    public void ResetImageCache()
    {
        var epoch = Interlocked.Increment(ref _generationEpoch);

        // Notify the coordinator that work for this tile at the old revision
        // is no longer needed. We remove by tile ID across all old revisions.
        var oldRevision = epoch - 1;
        var claimant = GetClaimantId();
        for (var mip = 0; mip <= BackgroundTileMipPolicy.MaxMipLevel; mip++)
        {
            var oldKey = new BackgroundTileCacheKey("synthetic", Id, oldRevision, mip);
            _coordinator?.RemoveClaimant(oldKey, claimant);
            ReleaseReservedCacheEntry?.Invoke(oldKey);
        }

        lock (_cacheGate)
        {
            _pixels = null;
            _mipPixels.Clear();
            _mipGenerationQueued.Clear();
            Interlocked.Exchange(ref _generationQueued, 0);
#if WINDOWS
            Interlocked.Exchange(ref _backgroundFetched, 0);
#endif
        }
    }

    /// <summary>
    /// Optional coordinator for bounded, cancellable tile generation.
    /// When set, <see cref="EnsurePixelsGenerationStarted"/> and
    /// <see cref="EnsureMipPixelsGenerationStarted"/> route work through
    /// the coordinator instead of using bare <c>Task.Run</c>.
    /// </summary>
    /// <summary>
    /// Optional coordinator for bounded, cancellable tile generation.
    /// When set, <see cref="EnsurePixelsGenerationStarted"/> and
    /// <see cref="EnsureMipPixelsGenerationStarted"/> route work through
    /// the coordinator instead of using bare <c>Task.Run</c>.
    /// </summary>
    public TileWorkCoordinator? Coordinator
    {
        get => _coordinator;
        set => _coordinator = value;
    }

    private object GetClaimantId() => ClaimantIdProvider?.Invoke() ?? _perTileClaimant;

    private CancellationToken GetClaimantToken() => ClaimantTokenProvider?.Invoke() ?? CancellationToken.None;

    public IReadOnlyList<SampleAnnotation> Annotations { get; }

    public bool ShouldGenerateForPixelSize(CameraSnapshot camera, double minimumPixelSize)
    {
        if (!double.IsFinite(minimumPixelSize) || minimumPixelSize <= 0)
        {
            return true;
        }

        if (IsImageGenerated)
        {
            return true;
        }

        if (!double.IsFinite(camera.ScaleX)
            || !double.IsFinite(camera.ScaleY)
            || camera.ScaleX <= 0
            || camera.ScaleY <= 0)
        {
            return false;
        }

        var topLeft = camera.WorldToScreen(Bounds.X, Bounds.Y);
        var bottomRight = camera.WorldToScreen(Bounds.Right, Bounds.Bottom);
        var projectedWidth = Math.Max(0, bottomRight.X - topLeft.X);
        var projectedHeight = Math.Max(0, bottomRight.Y - topLeft.Y);
        return Math.Min(projectedWidth, projectedHeight) >= minimumPixelSize;
    }

    public bool TryGetPixelValue(double worldX, double worldY, out byte value)
    {
        value = default;
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        if (worldX < Bounds.X || worldX >= Bounds.Right || worldY < Bounds.Y || worldY >= Bounds.Bottom)
        {
            return false;
        }

        var sourceX = Math.Clamp(
            (int)((worldX - Bounds.X) * PixelWidth / Bounds.Width),
            0,
            PixelWidth - 1);
        var sourceY = Math.Clamp(
            (int)((worldY - Bounds.Y) * PixelHeight / Bounds.Height),
            0,
            PixelHeight - 1);

        if (!TryGetPixelsNonBlocking(out var pixels))
        {
            value = _placeholderValue;
            return true;
        }

        value = pixels[(sourceY * PixelWidth) + sourceX];
        return true;
    }

    public bool IsCacheEntryGenerated(BackgroundTileCacheKey key)
    {
        if (!string.Equals(key.TileId, Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (_cacheGate)
        {
            return key.MipLevel == 0 ? _pixels is not null : _mipPixels.ContainsKey(key.MipLevel);
        }
    }

    public void EvictCacheEntry(BackgroundTileCacheKey key)
    {
        if (!string.Equals(key.TileId, Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_coordinator is not null)
        {
            _coordinator.RemoveClaimant(key, GetClaimantId());
        }

        lock (_cacheGate)
        {
            if (key.MipLevel == 0)
            {
                _pixels = null;
                Interlocked.Exchange(ref _generationQueued, 0);
#if WINDOWS
                Interlocked.Exchange(ref _backgroundFetched, 0);
#endif
            }
            else
            {
                _mipPixels.Remove(key.MipLevel);
                _mipGenerationQueued.Remove(key.MipLevel);
            }
        }
    }

    private static long GetByteCostForMip(int nativeWidth, int nativeHeight, int mipLevel)
    {
        var dimensions = BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel);
        return checked((long)dimensions.Width * dimensions.Height);
    }

    private void EnsurePixelsGenerationStarted(Func<BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry)
    {
        if (Interlocked.CompareExchange(ref _generationQueued, 1, 0) != 0)
        {
            return;
        }

        var key = new BackgroundTileCacheKey("synthetic", Id, Volatile.Read(ref _generationEpoch), 0);
        var reservationBytes = GetByteCostForMip(PixelWidth, PixelHeight, 0);

        if (_coordinator is not null)
        {
            var claimantToken = GetClaimantToken();
            var admitted = _coordinator.Request(
                key,
                async token =>
                {
                    var generationStarted = Stopwatch.GetTimestamp();
                    var result = _pixelFactory(token);
                    Interlocked.Exchange(ref _generationDurationTicks, Stopwatch.GetTimestamp() - generationStarted);
                    return result;
                },
                GetClaimantId(),
                claimantToken,
                onCompleted: OnCoordinatorPixelsGenerated,
                onFailed: OnCoordinatorPixelsGenerationFailed,
                tryReserve: tryReserveCacheEntry is null
                    ? null
                    : reserveKey => tryReserveCacheEntry(reserveKey, reservationBytes));

            if (!admitted)
            {
                Interlocked.Exchange(ref _generationQueued, 0);
            }
            else
            {
                // The coordinator removes a claimant without a callback when the
                // frame token fires. Without this reset, the dedup flag would
                // survive the claim and the tile would never re-request (ICW-204).
                RegisterClaimantReset(claimantToken);
            }

            return;
        }

        var reservation = tryReserveCacheEntry?.Invoke(key, reservationBytes);
        if (tryReserveCacheEntry is not null && reservation is null)
        {
            Interlocked.Exchange(ref _generationQueued, 0);
            return;
        }

        _ = Task.Run(() =>
        {
            var generationStarted = Stopwatch.GetTimestamp();
            var generationEpoch = Volatile.Read(ref _generationEpoch);
            try
            {
                var generated = _pixelFactory(CancellationToken.None);
                var shouldRaiseEvent = false;
                lock (_cacheGate)
                {
                    if (_pixels is null && generationEpoch == Volatile.Read(ref _generationEpoch))
                    {
                        _pixels = generated;
                        Interlocked.Exchange(ref _generationDurationTicks, Stopwatch.GetTimestamp() - generationStarted);
#if WINDOWS
                        Interlocked.Exchange(ref _backgroundFetched, 1);
#endif
                        shouldRaiseEvent = true;
                    }
                }

                if (shouldRaiseEvent)
                {
                    PixelsGenerated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    reservation?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _generationQueued, 0);
                reservation?.Dispose();
                try
                {
                    Serilog.Log.Error(ex, "Pixel generation failed for tile {TileId}", Id);
                }
                catch { }
                PixelsGenerationFailed?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnCoordinatorPixelsGenerated(BackgroundTileCacheKey key, byte[] pixels)
    {
        var expectedEpoch = key.ContentRevision;
        var currentEpoch = Volatile.Read(ref _generationEpoch);

        // Stale-generation publication guard (ICW-P0-STALE-PUB):
        // The key's ContentRevision captures the tile's _generationEpoch at
        // request time. If the tile was reset or evicted (epoch advanced)
        // between request and completion, the result is stale and must be
        // discarded. This shares the same epoch mechanism used by the frame-
        // level RenderRequestTracker (ICW-100): both compare a captured
        // version against the current version to detect staleness.
        var published = false;
        lock (_cacheGate)
        {
            if (_pixels is null && expectedEpoch == currentEpoch)
            {
                _pixels = pixels;
#if WINDOWS
                Interlocked.Exchange(ref _backgroundFetched, 1);
#endif
                published = true;
            }
            else
            {
                // Pixels discarded — epoch mismatch (tile was reset/evicted after
                // the request was made) or pixels already present. Reset the
                // generation-queued flag so the tile can retry generation.
                Interlocked.Exchange(ref _generationQueued, 0);
            }
        }

        if (!published)
        {
            ReleaseReservedCacheEntry?.Invoke(key);
            Log.Debug("TileGen DISCARD {TileId} mip{MipLevel} expectedEpoch={ExpectedEpoch} currentEpoch={CurrentEpoch} pixelsAlreadySet={PixelsSet}",
                Id, key.MipLevel, expectedEpoch, currentEpoch, _pixels is not null);
        }

        // Always fire the event so the render pipeline stays active and can
        // retry generation for tiles that were discarded. Without this, a
        // frame where all completions are stale would stop the render loop.
        PixelsGenerated?.Invoke(this, EventArgs.Empty);
    }

    private void OnCoordinatorPixelsGenerationFailed(BackgroundTileCacheKey key, Exception ex)
    {
        if (key.MipLevel == 0)
        {
            Interlocked.Exchange(ref _generationQueued, 0);
        }
        else
        {
            // Clear the per-mip dedup flag so a failed mip can retry (ICW-204).
            lock (_cacheGate)
            {
                _mipGenerationQueued.Remove(key.MipLevel);
            }
        }

        Log.Warning(ex, "TileGen FAIL {TileId} mip{MipLevel} rev{Rev}: {Reason}",
            Id, key.MipLevel, key.ContentRevision, ex.Message);
        PixelsGenerationFailed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets the native generation-queued flag when the claimant token fires.
    /// The coordinator removes a claimant without a callback when a frame token
    /// is cancelled (ICW-204). Without this reset the dedup flag survives the
    /// claim, so the tile never re-requests generation after a frame boundary.
    /// </summary>
    private void RegisterClaimantReset(CancellationToken claimantToken)
    {
        if (!claimantToken.CanBeCanceled)
        {
            return;
        }

        claimantToken.Register(() => Interlocked.Exchange(ref _generationQueued, 0));
    }

    /// <summary>
    /// Clears the per-mip generation-queued flag when the claimant token fires.
    /// See <see cref="RegisterClaimantReset(CancellationToken)"/>.
    /// </summary>
    private void RegisterClaimantReset(int mipLevel, CancellationToken claimantToken)
    {
        if (!claimantToken.CanBeCanceled)
        {
            return;
        }

        claimantToken.Register(() =>
        {
            lock (_cacheGate)
            {
                _mipGenerationQueued.Remove(mipLevel);
            }
        });
    }

    private void EnsureMipPixelsGenerationStarted(int mipLevel, Func<BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry)
    {
        lock (_cacheGate)
        {
            if (!_mipGenerationQueued.Add(mipLevel))
            {
                return;
            }
        }

        var key = new BackgroundTileCacheKey("synthetic", Id, Volatile.Read(ref _generationEpoch), mipLevel);
        var reservationBytes = GetByteCostForMip(PixelWidth, PixelHeight, mipLevel);

        if (_coordinator is not null)
        {
            var capturedMipLevel = mipLevel;
            var claimantToken = GetClaimantToken();
            var admitted = _coordinator.Request(
                key,
                async token =>
                {
                    var result = _mipPixelFactory!(capturedMipLevel, token);
                    return result;
                },
                GetClaimantId(),
                claimantToken,
                onCompleted: OnCoordinatorMipGenerated,
                onFailed: OnCoordinatorPixelsGenerationFailed,
                tryReserve: tryReserveCacheEntry is null
                    ? null
                    : reserveKey => tryReserveCacheEntry(reserveKey, reservationBytes));

            if (!admitted)
            {
                lock (_cacheGate)
                {
                    _mipGenerationQueued.Remove(mipLevel);
                }
            }
            else
            {
                // See RegisterClaimantReset(CancellationToken).
                RegisterClaimantReset(capturedMipLevel, claimantToken);
            }

            return;
        }

        var reservation = tryReserveCacheEntry?.Invoke(key, reservationBytes);
        if (tryReserveCacheEntry is not null && reservation is null)
        {
            lock (_cacheGate)
            {
                _mipGenerationQueued.Remove(mipLevel);
            }

            return;
        }

        _ = Task.Run(() =>
        {
            var generationEpoch = Volatile.Read(ref _generationEpoch);
            try
            {
                var generated = _mipPixelFactory!(mipLevel, CancellationToken.None);
                var shouldRaiseEvent = false;
                lock (_cacheGate)
                {
                    if (generationEpoch == Volatile.Read(ref _generationEpoch))
                    {
                        _mipPixels[mipLevel] = generated;
                        shouldRaiseEvent = true;
                    }

                    _mipGenerationQueued.Remove(mipLevel);
                }

                if (shouldRaiseEvent)
                {
                    PixelsGenerated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    reservation?.Dispose();
                }
            }
            catch (Exception ex)
            {
                lock (_cacheGate)
                {
                    _mipGenerationQueued.Remove(mipLevel);
                }
                reservation?.Dispose();
                try
                {
                    Serilog.Log.Error(ex, "Mip generation failed for tile {TileId} mip {MipLevel}", Id, mipLevel);
                }
                catch { }
                PixelsGenerationFailed?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnCoordinatorMipGenerated(BackgroundTileCacheKey key, byte[] pixels)
    {
        var expectedEpoch = key.ContentRevision;
        var currentEpoch = Volatile.Read(ref _generationEpoch);
        var mipLevel = key.MipLevel;
        var published = false;

        lock (_cacheGate)
        {
            if (expectedEpoch == currentEpoch)
            {
                _mipPixels[mipLevel] = pixels;
                published = true;
            }

            _mipGenerationQueued.Remove(mipLevel);
        }

        if (!published)
        {
            ReleaseReservedCacheEntry?.Invoke(key);
            Log.Debug("TileGen DISCARD mip {TileId} mip{MipLevel} expectedEpoch={ExpectedEpoch} currentEpoch={CurrentEpoch}",
                Id, mipLevel, expectedEpoch, currentEpoch);
        }

        // Always fire the event so the render pipeline stays active.
        PixelsGenerated?.Invoke(this, EventArgs.Empty);
    }

    private static byte[] ValidatePixels(byte[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height))
        {
            throw new InvalidOperationException("Generated pixel data length must match the image dimensions.");
        }

        return pixels;
    }

    private static byte[] ValidateMipPixels(byte[] pixels, int nativeWidth, int nativeHeight, int mipLevel)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var dimensions = BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel);
        if (pixels.Length != checked(dimensions.Width * dimensions.Height))
        {
            throw new InvalidOperationException("Generated mip pixel data length must match canonical dimensions.");
        }

        return pixels;
    }

    private static TimeSpan DurationFromStopwatchTicks(long ticks) =>
        TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency);

#if WINDOWS
#endif
}

public class SampleAnnotation : ISpatialEntity
{
    public string Id { get; }
    public string TileId { get; }
    public string ObjectId {get; }
    public SpatialBounds Bounds {get; }
    public Bgra32Color Color {get; }
    public string Classification {get; }

    private readonly Lazy<IReadOnlyDictionary<string, object>> _featuresLazy;

    public SampleAnnotation(string id, string tileId, string objectId, SpatialBounds bounds, Bgra32Color color, string classification, Func<IReadOnlyDictionary<string, object>> features, int defectPixelWidth, int defectPixelHeight, byte[] defectPixels)
    {
        Id = id;
        TileId = tileId;
        ObjectId = objectId;
        Bounds = bounds;
        Color = color;
        Classification = classification;
        _featuresLazy = new(features);
        DefectPixelWidth = defectPixelWidth;
        DefectPixelHeight = defectPixelHeight;
        DefectPixels = defectPixels;
    }

    public IReadOnlyDictionary<string, object> Features => _featuresLazy.Value;
    public int DefectPixelWidth {get; }
    public int DefectPixelHeight {get; }
    public byte[] DefectPixels {get; }

#if WINDOWS
    public Bitmap? DefectBitmap { get; init; }
#endif

    public bool TryGetDefectValue(double worldX, double worldY, out byte value)
    {
        value = default;
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        var localX = worldX - Bounds.X;
        var localY = worldY - Bounds.Y;
        var imageLeft = (Bounds.Width - DefectPixelWidth) / 2.0;
        var imageTop = (Bounds.Height - DefectPixelHeight) / 2.0;
        var imageRight = imageLeft + DefectPixelWidth;
        var imageBottom = imageTop + DefectPixelHeight;
        if (localX < imageLeft || localX >= imageRight || localY < imageTop || localY >= imageBottom)
        {
            return false;
        }

        var sourceX = Math.Clamp((int)(localX - imageLeft), 0, DefectPixelWidth - 1);
        var sourceY = Math.Clamp((int)(localY - imageTop), 0, DefectPixelHeight - 1);

        value = DefectPixels[(sourceY * DefectPixelWidth) + sourceX];
        return true;
    }

    public IReadOnlyList<FeatureDisplayItem> GetFeatureDisplayItems()
    {
        return AnnotationFeaturePresenter.BuildRows(this);
    }
}

public sealed record FeatureDisplayItem(string Name, string Value);

public sealed class TileCacheBudget
{
    public const long DefaultMaxBytes = 4L * 1024 * 1024 * 1024;

    private readonly long _maxBytes;
    private readonly Dictionary<BackgroundTileCacheKey, CacheEntry> _trackedEntries = new();
    private readonly HashSet<string> _pinnedTileIds = new(StringComparer.OrdinalIgnoreCase);
    private long _usedBytes;
    private int _evictionCount;

    public TileCacheBudget(long maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        _maxBytes = maxBytes;
    }

    public long MaxBytes => _maxBytes;

    public long UsedBytes => Volatile.Read(ref _usedBytes);

    public int EvictionCount => Volatile.Read(ref _evictionCount);

    public int ResidentTileCount
    {
        get
        {
            lock (_trackedEntries)
            {
                return _trackedEntries.Values
                    .Select(entry => entry.Tile.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
            }
        }
    }

    public int ResidentVariantCount
    {
        get
        {
            lock (_trackedEntries)
            {
                return _trackedEntries.Count;
            }
        }
    }

    public void SetPinnedTiles(IEnumerable<SampleImageTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        lock (_trackedEntries)
        {
            _pinnedTileIds.Clear();
            foreach (var tile in tiles)
            {
                _pinnedTileIds.Add(tile.Id);
            }
        }
    }

    public ICacheReservation? TryReserve(SampleImageTile tile, BackgroundTileCacheKey key, long byteCost)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (byteCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCost));
        }

        var evicted = new List<CacheEntry>();

        lock (_trackedEntries)
        {
            if (_trackedEntries.ContainsKey(key))
            {
                return NoOpReservation.Instance;
            }

            var entry = new CacheEntry(key, tile, byteCost);
            _trackedEntries[key] = entry;
            Interlocked.Add(ref _usedBytes, byteCost);

            while (UsedBytes > _maxBytes)
            {
                // Prefer evicting generated tiles (they can be regenerated).
                // Fall back to un-generated tiles if no generated ones are
                // available — un-generated tiles hold budget but provide
                // nothing to display.
                var evictedEntry = _trackedEntries.Values.FirstOrDefault(candidate =>
                    !candidate.Key.Equals(key)
                    && !_pinnedTileIds.Contains(candidate.Tile.Id)
                    && candidate.Tile.IsCacheEntryGenerated(candidate.Key))
                    ?? _trackedEntries.Values.FirstOrDefault(candidate =>
                        !candidate.Key.Equals(key)
                        && !_pinnedTileIds.Contains(candidate.Tile.Id));

                if (evictedEntry is null)
                {
                    _trackedEntries.Remove(key);
                    Interlocked.Add(ref _usedBytes, -byteCost);
                    Log.Warning("Cache EVICT REJECTED: no evictable entries. Tile={TileId} keyMip={MipLevel} cost={Cost} used={UsedBytes} max={MaxBytes} pinned={PinnedCount} tracked={TrackedCount}",
                        tile.Id, key.MipLevel, byteCost, UsedBytes, _maxBytes, _pinnedTileIds.Count, _trackedEntries.Count);
                    return null;
                }

                _trackedEntries.Remove(evictedEntry.Key);
                Interlocked.Add(ref _usedBytes, -evictedEntry.ByteCost);
                Log.Debug("Cache EVICT {EvictedTileId} mip{EvictedMip} cost={EvictedCost} generated={WasGenerated} (to admit {NewTileId} mip{NewMip} cost={NewCost}) used={UsedBytes} max={MaxBytes} evictions={EvictionCount}",
                    evictedEntry.Tile.Id,
                    evictedEntry.Key.MipLevel,
                    evictedEntry.ByteCost,
                    evictedEntry.Tile.IsCacheEntryGenerated(evictedEntry.Key),
                    tile.Id,
                    key.MipLevel,
                    byteCost,
                    UsedBytes,
                    _maxBytes,
                    _evictionCount + 1);
                evicted.Add(evictedEntry);
                Interlocked.Increment(ref _evictionCount);
            }
        }

        foreach (var evictedEntry in evicted)
        {
            evictedEntry.Tile.EvictCacheEntry(evictedEntry.Key);
        }

        return new CacheReservation(this, key);
    }

    public void Release(BackgroundTileCacheKey key)
    {
        lock (_trackedEntries)
        {
            if (!_trackedEntries.Remove(key, out var entry))
            {
                return;
            }

            Interlocked.Add(ref _usedBytes, -entry.ByteCost);
        }
    }

    public void Clear()
    {
        lock (_trackedEntries)
        {
            _trackedEntries.Clear();
            _pinnedTileIds.Clear();
            Interlocked.Exchange(ref _usedBytes, 0);
            Interlocked.Exchange(ref _evictionCount, 0);
        }
    }

    public string DescribeStatus()
    {
        return $"Cache {FormatBytes(UsedBytes)}/{FormatBytes(_maxBytes)}  |  {ResidentTileCount:N0} tiles  |  {ResidentVariantCount:N0} variants  |  {EvictionCount:N0} evictions";
    }

    private static string FormatBytes(long bytes)
    {
        const long gibibyte = 1024L * 1024 * 1024;
        return $"{bytes / (double)gibibyte:F2} GiB";
    }

    private sealed record CacheEntry(
        BackgroundTileCacheKey Key,
        SampleImageTile Tile,
        long ByteCost);

    private sealed class NoOpReservation : ICacheReservation
    {
        public static NoOpReservation Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class CacheReservation : ICacheReservation
    {
        private readonly TileCacheBudget _owner;
        private readonly BackgroundTileCacheKey _key;
        private int _disposed;

        public CacheReservation(TileCacheBudget owner, BackgroundTileCacheKey key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.Release(_key);
        }
    }
}