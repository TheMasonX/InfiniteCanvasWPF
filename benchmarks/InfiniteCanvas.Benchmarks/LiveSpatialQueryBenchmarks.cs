using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class LiveSpatialQueryBenchmarks
{
    private BlockingSpatialIndexBuilder _builder = null!;
    private LiveSpatialIndexService<BenchmarkEntity> _service = null!;
    private Task? _publicationTask;
    private SpatialBounds _viewport;

    [Params(LiveBufferState.SnapshotOnly, LiveBufferState.Hot, LiveBufferState.Publishing)]
    public LiveBufferState BufferState { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _builder = new BlockingSpatialIndexBuilder();
        _service = new LiveSpatialIndexService<BenchmarkEntity>(_builder);
        _service.AddRange(BenchmarkData.CreateUniformEntities(100_000));
        await _service.PublishSnapshotAsync();

        var bufferedItems = BenchmarkData.CreateUniformEntities(1_000, 100_000);
        if (BufferState == LiveBufferState.Hot)
        {
            _service.AddRange(bufferedItems);
        }
        else if (BufferState == LiveBufferState.Publishing)
        {
            _builder.BlockNextBuild();
            _service.AddRange(bufferedItems);
            _publicationTask = _service.PublishSnapshotAsync();
            await _builder.WaitForBlockedBuildAsync();
        }

        _viewport = BenchmarkData.CreateViewport(0.01);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _builder.ReleaseBuild();
        if (_publicationTask is not null)
        {
            await _publicationTask;
        }

        _builder.Dispose();
    }

    [Benchmark]
    public IReadOnlyList<BenchmarkEntity> Query()
    {
        return _service.Query(_viewport);
    }

    private sealed class BlockingSpatialIndexBuilder : ISpatialIndexBuilder<BenchmarkEntity>, IDisposable
    {
        private readonly ManualResetEventSlim _releaseBuild = new(initialState: true);
        private TaskCompletionSource _buildBlocked = CreateSignal();
        private int _blockNextBuild;

        public ISpatialIndexService<BenchmarkEntity> Build(IReadOnlyList<BenchmarkEntity> items)
        {
            if (Interlocked.Exchange(ref _blockNextBuild, 0) == 1)
            {
                _buildBlocked.TrySetResult();
                _releaseBuild.Wait();
            }

            return new StrTreeSpatialIndexService<BenchmarkEntity>(items);
        }

        public void BlockNextBuild()
        {
            _buildBlocked = CreateSignal();
            _releaseBuild.Reset();
            Interlocked.Exchange(ref _blockNextBuild, 1);
        }

        public Task WaitForBlockedBuildAsync()
        {
            return _buildBlocked.Task;
        }

        public void ReleaseBuild()
        {
            _releaseBuild.Set();
        }

        public void Dispose()
        {
            _releaseBuild.Dispose();
        }

        private static TaskCompletionSource CreateSignal()
        {
            return new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

public enum LiveBufferState
{
    SnapshotOnly,
    Hot,
    Publishing
}