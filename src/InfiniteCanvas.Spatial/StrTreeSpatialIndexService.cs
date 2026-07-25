using InfiniteCanvas.Core;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace InfiniteCanvas.Spatial;

public sealed class StrTreeSpatialIndexService<T> : ISpatialIndexService<T> where T : ISpatialEntity
{
    private readonly STRtree<T> _tree;

    public StrTreeSpatialIndexService(IEnumerable<T> items, int nodeCapacity = 10)
    {
        if (nodeCapacity <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
        }

        _tree = new STRtree<T>(nodeCapacity);

        foreach (var item in items)
        {
            _tree.Insert(ToEnvelope(item.Bounds), item);
            Count++;
        }

        _tree.Build();
    }

    public int Count { get; }

    public IReadOnlyList<T> Query(SpatialBounds viewport)
    {
        var results = _tree.Query(ToEnvelope(viewport));
        // NetTopologySuite returns a mutable `IList<T>`; copy to an array to ensure
        // callers receive an immutable snapshot and to avoid exposing internal lists.
        return results is T[] arr ? arr : results.ToArray();
    }

    private static Envelope ToEnvelope(SpatialBounds bounds)
    {
        return new Envelope(bounds.X, bounds.Right, bounds.Y, bounds.Bottom);
    }
}
