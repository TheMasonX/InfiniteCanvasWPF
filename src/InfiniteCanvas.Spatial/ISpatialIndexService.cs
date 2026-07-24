using InfiniteCanvas.Core;

namespace InfiniteCanvas.Spatial;

public interface ISpatialIndexService<T> where T : ISpatialEntity
{
    int Count { get; }

    IReadOnlyList<T> Query(SpatialBounds viewport);
}
