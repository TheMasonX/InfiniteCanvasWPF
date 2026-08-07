using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class StrTreeQueryBenchmarks
{
    private StrTreeSpatialIndexService<BenchmarkEntity>? _index = null;
    private SpatialBounds _viewport;

    [Params(100_000, 1_000_000, 10_000_000)]
    public int RecordCount { get; set; }

    [Params(0.001, 0.01, 0.1)]
    public double Selectivity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _index = new StrTreeSpatialIndexService<BenchmarkEntity>(
            BenchmarkData.CreateUniformEntities(RecordCount));
        _viewport = BenchmarkData.CreateViewport(Selectivity);
    }

    [Benchmark]
    public IReadOnlyList<BenchmarkEntity> Query()
    {
        return _index!.Query(_viewport);
    }
}