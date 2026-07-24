using InfiniteCanvas.Core;

namespace InfiniteCanvas.Spatial;

public sealed class LinearSpatialIndexBuilder<T> : ISpatialIndexBuilder<T> where T : ISpatialEntity
{
    public ISpatialIndexService<T> Build(IReadOnlyList<T> items)
    {
        return new ImmutableSpatialIndexService<T>(items);
    }
}
