namespace InfiniteCanvas.Core;

public sealed record SpatialRecord<TPayload>(string Id, SpatialBounds Bounds, TPayload Payload) : ISpatialEntity;
