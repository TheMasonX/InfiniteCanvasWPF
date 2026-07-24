using InfiniteCanvas.Core;

namespace InfiniteCanvas.Spatial;

public sealed class StrTreeSpatialIndexBuilder<T>(int nodeCapacity = 10) : ISpatialIndexBuilder<T>
    where T : ISpatialEntity
{
    public ISpatialIndexService<T> Build(IReadOnlyList<T> items)
    {
        return new StrTreeSpatialIndexService<T>(items, nodeCapacity);
    }
}
