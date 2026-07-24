using InfiniteCanvas.Core;

namespace InfiniteCanvas.Benchmarks;

internal static class BenchmarkData
{
    public const double WorldSize = 10_000;

    public static BenchmarkEntity[] CreateUniformEntities(int count, int idOffset = 0)
    {
        var entities = new BenchmarkEntity[count];
        for (var index = 0; index < count; index++)
        {
            var id = idOffset + index;
            var x = ((id * 7_919L) % 10_000_019) / 10_000_019d * WorldSize;
            var y = ((id * 3_571L) % 10_000_079) / 10_000_079d * WorldSize;
            entities[index] = new BenchmarkEntity(id, new SpatialBounds(x, y, 0, 0));
        }

        return entities;
    }

    public static SpatialBounds CreateViewport(double selectivity)
    {
        var sideLength = Math.Sqrt(selectivity) * WorldSize;
        var origin = (WorldSize - sideLength) / 2;
        return new SpatialBounds(origin, origin, sideLength, sideLength);
    }
}

public readonly record struct BenchmarkEntity(int Id, SpatialBounds Bounds) : ISpatialEntity;