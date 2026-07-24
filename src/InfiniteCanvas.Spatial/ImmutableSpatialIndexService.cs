using InfiniteCanvas.Core;
using System.Collections.Immutable;

namespace InfiniteCanvas.Spatial;

public sealed class ImmutableSpatialIndexService<T> : ISpatialIndexService<T> where T : ISpatialEntity
{
    private readonly ImmutableArray<T> _items;

    public ImmutableSpatialIndexService(IEnumerable<T> items)
    {
        _items = items.ToImmutableArray();
    }

    public int Count => _items.Length;

    public IReadOnlyList<T> Query(SpatialBounds viewport)
    {
        if (_items.IsDefaultOrEmpty)
        {
            return [];
        }

        var results = new List<T>();

        foreach (var item in _items)
        {
            if (item.Bounds.Intersects(viewport))
            {
                results.Add(item);
            }
        }

        return results;
    }
}
