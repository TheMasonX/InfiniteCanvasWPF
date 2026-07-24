using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class SnapshotBuildBenchmarks
{
    private BenchmarkEntity[] _entities = null!;
    private StrTreeSpatialIndexBuilder<BenchmarkEntity> _builder = null!;

    [Params(100_000, 1_000_000, 10_000_000)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _entities = BenchmarkData.CreateUniformEntities(RecordCount);
        _builder = new StrTreeSpatialIndexBuilder<BenchmarkEntity>();
    }

    [Benchmark]
    public ISpatialIndexService<BenchmarkEntity> BuildSnapshot()
    {
        return _builder.Build(_entities);
    }
}