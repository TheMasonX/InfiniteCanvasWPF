using InfiniteCanvas.Core;

namespace InfiniteCanvas.Spatial;

public interface ISpatialIndexBuilder<T> where T : ISpatialEntity
{
    ISpatialIndexService<T> Build(IReadOnlyList<T> items);
}
