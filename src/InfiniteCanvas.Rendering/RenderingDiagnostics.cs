using System.Diagnostics;

namespace InfiniteCanvas.Rendering;

public enum RenderingStage
{
    NativeNoiseGeneration,
    Gray8Normalization,
    CircleRasterization,
    TileComposition,
    SparseComposition
}

public enum RenderingDiagnosticOutcome
{
    Requested,
    Generated,
    Reused,
    ResidentFallback,
    Useful,
    Stale,
    Rejected,
    Failed,
    Evicted
}

public sealed record RenderingMipDiagnostics(
    long Requested,
    long Generated,
    long Reused,
    long ResidentFallback,
    long Useful,
    long Stale,
    long Rejected,
    long Failed,
    long Evicted,
    long SampleCount,
    long ResidentPayloadBytes);

public sealed record RenderingDiagnosticsSnapshot(
    IReadOnlyDictionary<RenderingStage, TimeSpan> StageDurations,
    IReadOnlyDictionary<RenderingStage, long> StageSamples,
    IReadOnlyDictionary<int, RenderingMipDiagnostics> MipLevels)
{
    public TimeSpan GetStageDuration(RenderingStage stage) =>
        StageDurations.TryGetValue(stage, out var duration) ? duration : TimeSpan.Zero;

    public long GetStageSampleCount(RenderingStage stage) =>
        StageSamples.TryGetValue(stage, out var samples) ? samples : 0;
}

public sealed class RenderingDiagnostics
{
    private static readonly AsyncLocal<RenderingDiagnostics?> CurrentScope = new();
    private readonly long[] _stageTicks = new long[Enum.GetValues<RenderingStage>().Length];
    private readonly long[] _stageSamples = new long[Enum.GetValues<RenderingStage>().Length];
    private readonly object _mipGate = new();
    private readonly Dictionary<int, MutableMipDiagnostics> _mipLevels = new();

    public static RenderingDiagnostics? Current => CurrentScope.Value;

    public static IDisposable Activate(RenderingDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var previous = CurrentScope.Value;
        CurrentScope.Value = diagnostics;
        return new Scope(() => CurrentScope.Value = previous);
    }

    public IDisposable Measure(RenderingStage stage, int mipLevel)
    {
        var started = Stopwatch.GetTimestamp();
        return new Measurement(this, stage, mipLevel, started);
    }

    public void Record(RenderingDiagnosticOutcome outcome, int mipLevel, long sampleCount = 0, long residentPayloadBytes = 0)
    {
        if (mipLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        lock (_mipGate)
        {
            var values = GetMip(mipLevel);
            values.Record(outcome, sampleCount, residentPayloadBytes);
        }
    }

    public RenderingDiagnosticsSnapshot Snapshot()
    {
        var stageDurations = new Dictionary<RenderingStage, TimeSpan>();
        var stageSamples = new Dictionary<RenderingStage, long>();
        foreach (var stage in Enum.GetValues<RenderingStage>())
        {
            stageDurations[stage] = TimeSpan.FromSeconds(
                Volatile.Read(ref _stageTicks[(int)stage]) / (double)Stopwatch.Frequency);
            stageSamples[stage] = Volatile.Read(ref _stageSamples[(int)stage]);
        }

        lock (_mipGate)
        {
            var mipLevels = _mipLevels.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToSnapshot());
            return new RenderingDiagnosticsSnapshot(stageDurations, stageSamples, mipLevels);
        }
    }

    internal static IDisposable? MeasureCurrent(RenderingStage stage, int mipLevel) =>
        Current?.Measure(stage, mipLevel);

    internal static void RecordCurrent(
        RenderingDiagnosticOutcome outcome,
        int mipLevel,
        long sampleCount = 0,
        long residentPayloadBytes = 0) =>
        Current?.Record(outcome, mipLevel, sampleCount, residentPayloadBytes);

    private MutableMipDiagnostics GetMip(int mipLevel)
    {
        if (!_mipLevels.TryGetValue(mipLevel, out var values))
        {
            values = new MutableMipDiagnostics();
            _mipLevels.Add(mipLevel, values);
        }

        return values;
    }

    private void AddMeasurement(RenderingStage stage, int mipLevel, long elapsedTicks)
    {
        Interlocked.Add(ref _stageTicks[(int)stage], elapsedTicks);
        Interlocked.Increment(ref _stageSamples[(int)stage]);
    }

    private sealed class Measurement(RenderingDiagnostics owner, RenderingStage stage, int mipLevel, long started) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.AddMeasurement(stage, mipLevel, Stopwatch.GetTimestamp() - started);
            }
        }
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class MutableMipDiagnostics
    {
        private long _requested;
        private long _generated;
        private long _reused;
        private long _residentFallback;
        private long _useful;
        private long _stale;
        private long _rejected;
        private long _failed;
        private long _evicted;
        private long _sampleCount;
        private long _residentPayloadBytes;

        public void Record(RenderingDiagnosticOutcome outcome, long sampleCount, long residentPayloadBytes)
        {
            switch (outcome)
            {
                case RenderingDiagnosticOutcome.Requested: Interlocked.Increment(ref _requested); break;
                case RenderingDiagnosticOutcome.Generated: Interlocked.Increment(ref _generated); break;
                case RenderingDiagnosticOutcome.Reused: Interlocked.Increment(ref _reused); break;
                case RenderingDiagnosticOutcome.ResidentFallback: Interlocked.Increment(ref _residentFallback); break;
                case RenderingDiagnosticOutcome.Useful: Interlocked.Increment(ref _useful); break;
                case RenderingDiagnosticOutcome.Stale: Interlocked.Increment(ref _stale); break;
                case RenderingDiagnosticOutcome.Rejected: Interlocked.Increment(ref _rejected); break;
                case RenderingDiagnosticOutcome.Failed: Interlocked.Increment(ref _failed); break;
                case RenderingDiagnosticOutcome.Evicted: Interlocked.Increment(ref _evicted); break;
            }

            if (sampleCount != 0)
            {
                Interlocked.Add(ref _sampleCount, sampleCount);
            }

            if (residentPayloadBytes != 0)
            {
                Interlocked.Exchange(ref _residentPayloadBytes, residentPayloadBytes);
            }
        }

        public RenderingMipDiagnostics ToSnapshot() => new(
            Volatile.Read(ref _requested),
            Volatile.Read(ref _generated),
            Volatile.Read(ref _reused),
            Volatile.Read(ref _residentFallback),
            Volatile.Read(ref _useful),
            Volatile.Read(ref _stale),
            Volatile.Read(ref _rejected),
            Volatile.Read(ref _failed),
            Volatile.Read(ref _evicted),
            Volatile.Read(ref _sampleCount),
            Volatile.Read(ref _residentPayloadBytes));
    }
}