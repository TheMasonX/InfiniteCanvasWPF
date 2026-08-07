namespace InfiniteCanvas.Rendering;

/// <summary>
/// Materializes source-neutral background payloads without blocking the caller.
/// </summary>
public sealed class BackgroundTileMaterializer : IDisposable
{
    private readonly IBackgroundTileSource _source;
    private readonly TileWorkCoordinator _coordinator;
    private readonly long _maxBytes;
    private readonly Lock _gate = new();
    private readonly Dictionary<BackgroundTileCacheKey, CacheEntry> _resident = new();
    private readonly Dictionary<BackgroundTileCacheKey, int> _inFlightEpochs = new();
    private readonly Dictionary<BackgroundTileCacheKey, long> _inFlightCosts = new();
    private readonly HashSet<BackgroundTileCacheKey> _pinnedKeys = new();
    private long _usedBytes;
    private int _sceneEpoch;
    private bool _disposed;

    public BackgroundTileMaterializer(IBackgroundTileSource source, TileWorkCoordinator coordinator, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(coordinator);
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        _source = source;
        _coordinator = coordinator;
        _maxBytes = maxBytes;
    }

    public long UsedBytes
    {
        get
        {
            lock (_gate)
            {
                return _usedBytes;
            }
        }
    }

    public int ResidentCount
    {
        get
        {
            lock (_gate)
            {
                return _resident.Count;
            }
        }
    }

    public void SetPinnedKeys(IEnumerable<BackgroundTileCacheKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pinnedKeys.Clear();
            _pinnedKeys.UnionWith(keys);
        }
    }

    public bool TryGetResident(BackgroundTileRequest request, out BackgroundTilePayload payload)
    {
        lock (_gate)
        {
            if (_resident.TryGetValue(request.CacheKey, out var entry))
            {
                payload = entry.Payload;
                return true;
            }
        }

        payload = null!;
        return false;
    }

    /// <summary>
    /// Requests a payload. Equal cache keys share one source operation.
    /// </summary>
    public bool Request(
        BackgroundTileRequest request,
        object claimantId,
        CancellationToken claimantToken,
        Action<BackgroundTilePayload>? onCompleted = null,
        Action<Exception>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(claimantId);

        var dimensions = request.CanonicalDimensions;
        var byteCost = checked((long)dimensions.Width * dimensions.Height);
        int requestEpoch;
        BackgroundTilePayload? residentPayload = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_resident.TryGetValue(request.CacheKey, out var resident))
            {
                residentPayload = resident.Payload;
                requestEpoch = _sceneEpoch;
            }
            else if (_inFlightEpochs.TryGetValue(request.CacheKey, out var existingEpoch))
            {
                requestEpoch = existingEpoch;
            }
            else
            {
                EvictForAdmission(byteCost, request.CacheKey);
                if (_usedBytes + byteCost > _maxBytes)
                {
                    return false;
                }

                _usedBytes += byteCost;
                requestEpoch = _sceneEpoch;
                _inFlightEpochs.Add(request.CacheKey, requestEpoch);
                _inFlightCosts.Add(request.CacheKey, byteCost);
            }
        }

        if (residentPayload is not null)
        {
            onCompleted?.Invoke(residentPayload);
            return true;
        }

        var accepted = _coordinator.Request(
            request.CacheKey,
            async token =>
            {
                var sourcePayload = await _source.ResolveAsync(request, token).ConfigureAwait(false);
                if (sourcePayload.Request.CacheKey != request.CacheKey)
                {
                    throw new InvalidOperationException("The source returned a payload for a different cache key.");
                }

                return sourcePayload.Pixels;
            },
            claimantId,
            claimantToken,
            onCompleted: (_, pixels) => Complete(request, requestEpoch, pixels, onCompleted),
            onFailed: (_, error) => Fail(request.CacheKey, requestEpoch, error, onFailed));

        if (!accepted)
        {
            ReleaseInFlight(request.CacheKey, requestEpoch);
        }

        return accepted;
    }

    public void AdvanceScene()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _sceneEpoch++;
            _resident.Clear();
            _inFlightEpochs.Clear();
            _inFlightCosts.Clear();
            _usedBytes = 0;
        }

        _coordinator.CancelAll();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _resident.Clear();
            _inFlightEpochs.Clear();
            _inFlightCosts.Clear();
            _usedBytes = 0;
        }

        _coordinator.CancelAll();
    }

    private void Complete(BackgroundTileRequest request, int requestEpoch, byte[] pixels, Action<BackgroundTilePayload>? onCompleted)
    {
        try
        {
            var payload = new BackgroundTilePayload(request, pixels);
            lock (_gate)
            {
                if (_disposed || requestEpoch != _sceneEpoch)
                {
                    ReleaseInFlight(request.CacheKey, requestEpoch);
                    return;
                }

                _inFlightEpochs.Remove(request.CacheKey);
                _inFlightCosts.Remove(request.CacheKey);
                _resident[request.CacheKey] = new CacheEntry(payload);
            }

            onCompleted?.Invoke(payload);
        }
        catch (Exception error)
        {
            Fail(request.CacheKey, requestEpoch, error, null);
        }
    }

    private void Fail(BackgroundTileCacheKey key, int requestEpoch, Exception error, Action<Exception>? onFailed)
    {
        ReleaseInFlight(key, requestEpoch);
        onFailed?.Invoke(error);
    }

    private void ReleaseInFlight(BackgroundTileCacheKey key, int requestEpoch)
    {
        lock (_gate)
        {
            if (_inFlightEpochs.TryGetValue(key, out var currentEpoch)
                && currentEpoch == requestEpoch
                && _inFlightEpochs.Remove(key))
            {
                _usedBytes -= _inFlightCosts[key];
                _inFlightCosts.Remove(key);
            }
        }
    }

    private void EvictForAdmission(long byteCost, BackgroundTileCacheKey requestedKey)
    {
        while (_usedBytes + byteCost > _maxBytes)
        {
            var candidate = _resident.FirstOrDefault(pair =>
                !pair.Key.Equals(requestedKey) && !_pinnedKeys.Contains(pair.Key));
            if (candidate.Value is null)
            {
                return;
            }

            _resident.Remove(candidate.Key);
            _usedBytes -= candidate.Value.Payload.ByteCost;
        }
    }

    private sealed record CacheEntry(BackgroundTilePayload Payload);
}