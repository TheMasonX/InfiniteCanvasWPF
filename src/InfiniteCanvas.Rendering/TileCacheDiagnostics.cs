namespace InfiniteCanvas.Rendering;

public sealed record TileCacheVariantDiagnostics(
    BackgroundTileCacheKey Key,
    long ByteCost,
    bool IsGenerated);

public sealed record TileCacheDiagnosticsSnapshot(
    Guid ActiveCacheId,
    int ResidentCount,
    IReadOnlyList<TileCacheVariantDiagnostics> ResidentVariants,
    int QueuedWorkCount,
    int ReservationCount,
    int EvictionCount,
    DateTimeOffset LastResetAtUtc);
