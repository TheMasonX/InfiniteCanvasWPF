#if WINDOWS
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using BenchmarkDotNet.Attributes;

namespace InfiniteCanvas.Benchmarks;

/// <summary>
/// Measures the WPF visual lifecycle that remains after annotation retention.
/// The benchmark compares fresh detached-state allocation with bounded reuse.
/// Raster generation and camera projection are intentionally excluded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class AnnotationOverlayPoolingBenchmarks
{
    private const int CyclesPerInvocation = 16;

    private Thread _uiThread = null!;
    private Dispatcher _dispatcher = null!;
    private Canvas _annotationLayer = null!;
    private List<OverlayPair> _active = null!;
    private Stack<OverlayPair> _pool = null!;
    private readonly ManualResetEventSlim _dispatcherReady = new(false);

    [Params(64, 256)]
    public int AnnotationCount { get; set; }

    [Params(0.25, 1.0)]
    public double ChurnRatio { get; set; }

    private int ChurnCount => Math.Clamp(
        (int)Math.Round(AnnotationCount * ChurnRatio),
        1,
        AnnotationCount);

    [GlobalSetup]
    public void StartDispatcher()
    {
        _uiThread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _annotationLayer = new Canvas();
            _active = [];
            _pool = [];
            _dispatcherReady.Set();
            Dispatcher.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();
        _dispatcherReady.Wait();
    }

    [IterationSetup]
    public void ResetScene()
    {
        InvokeOnUi(() =>
        {
            _annotationLayer.Children.Clear();
            _active.Clear();
            _pool.Clear();
            for (var index = 0; index < AnnotationCount; index++)
            {
                var pair = CreateOverlayPair(index);
                _active.Add(pair);
                AddPair(pair);
            }
        });
    }

    [Benchmark(Baseline = true)]
    public void RecreateDetachedStates()
    {
        InvokeOnUi(() => RunCycles(reuseDetachedStates: false));
    }

    [Benchmark]
    public void ReuseDetachedStates()
    {
        InvokeOnUi(() => RunCycles(reuseDetachedStates: true));
    }

    [GlobalCleanup]
    public void StopDispatcher()
    {
        _dispatcher.InvokeShutdown();
        _uiThread.Join();
        _dispatcherReady.Dispose();
    }

    private void RunCycles(bool reuseDetachedStates)
    {
        var churnCount = ChurnCount;
        for (var cycle = 0; cycle < CyclesPerInvocation; cycle++)
        {
            for (var index = 0; index < churnCount; index++)
            {
                var pair = _active[index];
                RemovePair(pair);
                if (reuseDetachedStates)
                {
                    _pool.Push(pair);
                }
            }

            for (var index = 0; index < churnCount; index++)
            {
                var pair = reuseDetachedStates && _pool.Count > 0
                    ? _pool.Pop()
                    : CreateOverlayPair(index);
                pair.Element.Tag = index;
                if (pair.Label.Child is TextBlock label)
                {
                    label.Text = $"A{index}";
                }

                AddPair(pair);
                _active[index] = pair;
            }
        }
    }

    private void AddPair(OverlayPair pair)
    {
        _annotationLayer.Children.Add(pair.Element);
        _annotationLayer.Children.Add(pair.Label);
    }

    private void RemovePair(OverlayPair pair)
    {
        _annotationLayer.Children.Remove(pair.Element);
        _annotationLayer.Children.Remove(pair.Label);
    }

    private static OverlayPair CreateOverlayPair(int index)
    {
        var element = new Border
        {
            Child = new Rectangle
            {
                Width = 32,
                Height = 16
            },
            Tag = index
        };
        var label = new Border
        {
            Child = new TextBlock
            {
                Text = $"A{index}"
            }
        };
        return new OverlayPair(element, label);
    }

    private void InvokeOnUi(Action action) => _dispatcher.Invoke(action);

    private sealed record OverlayPair(Border Element, Border Label);
}
#endif