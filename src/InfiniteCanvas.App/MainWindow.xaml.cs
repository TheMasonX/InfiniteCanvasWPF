using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using InfiniteCanvas.App.Controls;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;
using Serilog;

namespace InfiniteCanvas.App;

public partial class MainWindow : Window, ICanvasSceneSource, ICanvasSpatialQuerySource
{
    private LiveSpatialIndexService<SampleAnnotation> _spatialIndex = null!;
    private CameraTransform _camera = null!;
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _resizeTimer;
    private readonly ISelectionOutlineAnimator _selectionOutlineAnimator;
    private MainViewModel _mainViewModel = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _generationGate = new(1, 1);
    private readonly string _settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InfiniteCanvas",
        "settings.json");
    private IReadOnlyList<SampleImageTile> _tiles = [];
    private IReadOnlyDictionary<string, SpatialBounds> _tileBoundsById = new Dictionary<string, SpatialBounds>();
    private IReadOnlyList<SampleAnnotation> _annotations = [];
    private SpatialBounds _sceneBounds;
    private readonly FrameBufferPool _frameBufferPool = new();
    private CameraSnapshot _lastPublishedCamera;
    private Point? _hoverPointerPosition;
    private string? _selectedAnnotationId;
    private AnnotationDisplayOptions _annotationDisplayOptions = AnnotationDisplayOptions.Default;
    private int _tileColumns = 2;
    private int _tileRows = 32;
    private int _objectsPerTile = 16;
    private int _generationSeed = 1729;
    private int _busyOperationCount;
    private TileCacheBudget _tileCacheBudget = new(TileCacheBudget.DefaultMaxBytes);
    private bool _showBackgroundImages = true;
    private bool _showImageTiles = true;
    private IReadOnlyList<FeatureDisplayItem> _selectedAnnotationFeatures = [];
    private TileWorkCoordinator _tileCoordinator = null!;
    private int _frameClaimantId;
    private readonly RenderRequestTracker _renderRequestTracker = new();
    private CancellationTokenSource? _frameTileCts;
    private CancellationTokenSource? _previousFrameTileCts;
    private int _diagnosticsFrameCount;
    private readonly Stopwatch _diagnosticsStopwatch = Stopwatch.StartNew();
    private long _lastFrameTicks;
    private long _totalFrameTicks;
    private int _frameCount;

    private Border ViewportHost => CanvasSurface.SurfaceHost;
    private TextBlock LoadingOverlay => CanvasSurface.LoadingText;
    private TextBlock PixelometerWorldText => CanvasSurface.WorldReadout;
    private TextBlock PixelometerTileText => CanvasSurface.TileReadout;
    private TextBlock PixelometerValueText => CanvasSurface.ValueReadout;
    private ProgressBar RenderBusyBar => CanvasSurface.BusyBar;

    public IReadOnlyList<FeatureDisplayItem> SelectedAnnotationFeatures => _selectedAnnotationFeatures;

    public MainWindow()
    {
        InitializeComponent();

        _camera = CanvasSurface.ViewModel.Camera;
        CanvasSurface.ViewportChanged += OnCanvasViewportChanged;
        CanvasSurface.PointerMoved += OnCanvasPointerMoved;
        CanvasSurface.PointerWheel += OnCanvasPointerWheel;
        CanvasSurface.SizeChanged += OnViewportSizeChanged;
        // The canvas owns the frame shell and raster display (ICW-315). The
        // host keeps overlay composition and populates it per published frame.
        CanvasSurface.FramePublished += OnCanvasFramePublished;
        // The window is the concrete data-source implementation behind the
        // canvas boundary (ICW-312, ADR-0007). The control consumes content
        // only through these contracts, never through app types.
        CanvasSurface.SceneSource = this;
        CanvasSurface.SpatialQuerySource = this;

        // The MainViewModel is stable for the window lifetime. Regeneration
        // reuses it so user-edited settings never reset to defaults.
        DataContext = _mainViewModel;
        InitializeSpatialState();
        _tileCoordinator = new TileWorkCoordinator();
        _renderAction = new CoalescingAsyncAction(DispatchRenderFrameAsync, OnRenderActionFaulted);
        _selectionOutlineAnimator = SelectionOutlineAnimatorFactory.Create(SelectionOutlineAnimationMode.MarchingDash);

        _resizeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnResizeElapsed, Dispatcher)
        {
            IsEnabled = false
        };

        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
        ApplySettingsToUi(CanvasUserSettingsStore.Load(_settingsPath));

        Loaded += OnLoaded;
        Closed += OnClosed;
        // Drives the frame-buffer composition handoff (ICW-318). The pool only
        // reuses a retired buffer after the compositor has advanced past it.
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        _frameBufferPool.OnCompositionFrame();
    }

    private void InitializeSpatialState()
    {
        _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(new StrTreeSpatialIndexBuilder<SampleAnnotation>());
    }

    // --- ICanvasSceneSource / ICanvasSpatialQuerySource (ICW-312, ADR-0007) ---
    // The window implements the canvas data-source contracts. The canvas
    // consumes content only through these members, never through concrete
    // application types.

    public SpatialBounds SceneBounds => _sceneBounds;

    public int TotalItemCount => _spatialIndex.Count;

    public event EventHandler? SceneChanged;

    // One public method satisfies both contracts. ICanvasSceneSource and
    // ICanvasSpatialQuerySource expose the same QueryVisible signature.
    public IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport)
    {
        // IReadOnlyList<out T> is covariant, so the SampleAnnotation result
        // converts directly to IReadOnlyList<ICanvasItem>.
        return _spatialIndex.Query(viewport);
    }

    public bool TryReadResidentPixel(double worldX, double worldY, int mipLevel, out CanvasPixelSample sample)
    {
        // Non-blocking resident read: never initiates tile generation
        // (ICW-P0-PIXELOMETER-READOUT, closed by ICW-312).
        if (_tiles.Count == 0
            || !TileGridIndexLookup.TryGetTileIndex(
                worldX,
                worldY,
                _sceneBounds,
                _tiles[0].Bounds.Width,
                _tiles[0].Bounds.Height,
                _tileColumns,
                _tiles.Count,
                out var tileIndex))
        {
            sample = default;
            return false;
        }

        var tile = _tiles[tileIndex];

        byte background;
        if (tile.TryGetResidentPixels(mipLevel, out var sourcePixels, out var residentMipLevel))
        {
            var sourceDimensions = BackgroundTileMipPolicy.GetDimensions(tile.PixelWidth, tile.PixelHeight, residentMipLevel);
            var sourceX = Math.Clamp((int)((worldX - tile.Bounds.X) * sourceDimensions.Width / tile.Bounds.Width), 0, Math.Max(0, sourceDimensions.Width - 1));
            var sourceY = Math.Clamp((int)((worldY - tile.Bounds.Y) * sourceDimensions.Height / tile.Bounds.Height), 0, Math.Max(0, sourceDimensions.Height - 1));
            background = sourcePixels[(sourceY * sourceDimensions.Width) + sourceX];
        }
        else
        {
            background = tile.PlaceholderValue;
        }

        byte defect = 0;
        var sampleArea = new SpatialBounds(worldX, worldY, 0.01, 0.01);
        var hitAnnotations = _spatialIndex.Query(sampleArea);
        for (var index = 0; index < hitAnnotations.Count; index++)
        {
            if (hitAnnotations[index].TryGetDefectValue(worldX, worldY, out var value))
            {
                defect = Math.Max(defect, value);
            }
        }

        var dimensions = BackgroundTileMipPolicy.GetDimensions(tile.PixelWidth, tile.PixelHeight, mipLevel);
        sample = new CanvasPixelSample(
            background,
            defect,
            tile.Id,
            new BackgroundTileReadoutInfo(tile.Id, mipLevel, dimensions.Width, dimensions.Height).Format());
        return true;
    }

    private async void OnCanvasViewportChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || _lifetime.IsCancellationRequested)
        {
            return;
        }

        await RequestRenderAsync();
    }

    private void OnCanvasPointerMoved(object? sender, MouseEventArgs e)
    {
        _hoverPointerPosition = e.GetPosition(ViewportHost);
        UpdatePixelometer(_hoverPointerPosition.Value);
    }

    private void OnAboutButtonClicked(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            ApplyGenerationControlsToUi();
            ApplyDisplayOptionsFromUi();
            await RegenerateSceneAsync(fitToWidth: true);
        }
        catch (OperationCanceledException ex)
        {
            Log.Debug(ex, "OnLoaded canceled");
        }
        catch (Exception exception)
        {
            LoadingOverlay.Text = "INITIALIZATION FAILED";
            StatusText.Text = exception.Message;
        }
    }

    private void ApplyGenerationControlsToUi()
    {
        TilesXSliderTextBox.Value = _tileColumns;
        TilesYSliderTextBox.Value = _tileRows;
        ObjectsPerTileSliderTextBox.Value = _objectsPerTile;
        GenerationSeedSliderTextBox.Value = _generationSeed;
    }

    private void ApplySettingsToUi(CanvasUserSettings settings)
    {
        _tileColumns = settings.TileColumns;
        _tileRows = settings.TileRows;
        _objectsPerTile = settings.ObjectsPerTile;
        _generationSeed = settings.GenerationSeed;
        ApplyGenerationControlsToUi();
        DisplayModeComboBox.SelectedIndex = settings.AnnotationDisplayMode;
        OutlineThicknessSlider.Value = settings.OutlineThickness;
        LabelSizeSlider.Value = settings.LabelSize;
        LabelDisplayComboBox.SelectedIndex = settings.LabelDisplay;
        ShowLabelsCheckBox.IsChecked = settings.ShowLabels;
        ShowImageTilesCheckBox.IsChecked = settings.ShowImageTiles;
        _showImageTiles = settings.ShowImageTiles;
        ShowBackgroundImagesCheckBox.IsChecked = settings.ShowBackgroundImages;
        _showBackgroundImages = settings.ShowBackgroundImages;
        _mainViewModel.ApplySettings(settings);
        // The canvas owns the raster Image element; the host drives its
        // visibility from the layer-visibility settings (ICW-315).
        CanvasSurface.RasterVisible = _showBackgroundImages || _showImageTiles;
    }

    private async Task RegenerateSceneAsync(bool fitToWidth)
    {
        await _generationGate.WaitAsync(_lifetime.Token);
        BeginBusyOperation();
        try
        {
            LoadingOverlay.Text = "GENERATING TILE MATERIAL";
            LoadingOverlay.Visibility = Visibility.Visible;
            RegenerateButton.IsEnabled = false;

            InitializeSpatialState();
            _selectedAnnotationId = null;
            CanvasSurface.ResetCamera();
            _tileCacheBudget = new TileCacheBudget(_tileCacheBudget.MaxBytes);
            UnsubscribeTileGenerationEvents(_tiles);

            // Dispose defect template pools from the previous scene. The pool is
            // shared across all tiles in a generation set, so we collect unique
            // references and dispose them once at the scene boundary.
            // Cancel any in-flight tile generation from the previous scene.
            _tileCoordinator.CancelAll();

            SampleImageTile.DisposeDefectTemplatePools(_tiles);

            var tileCount = checked(_tileColumns * _tileRows);
            StatusText.Text = $"Generating metadata for {tileCount:N0} inspection tiles";
            SceneSummaryText.Text = $"{tileCount:N0} TILE INSPECTION SCENE ({_tileColumns} x {_tileRows})";

            // The noise settings view model is stable for the window lifetime,
            // so the generator reads the same snapshot that remains bound to UI.
            var backgroundNoiseSettings = _mainViewModel.CreateBackgroundNoiseSnapshot();
            _tiles = await Task.Run(
                () => SampleImageGenerator.GenerateSet(
                    imageCount: tileCount,
                    objectsPerTile: _objectsPerTile,
                    columns: _tileColumns,
                    rows: _tileRows,
                    seed: _generationSeed,
                    defectPoolSize: 64,
                    targetValue: backgroundNoiseSettings.TargetValue,
                    noise: backgroundNoiseSettings.Noise,
                    circleCount: backgroundNoiseSettings.CircleCount,
                    noiseScale: backgroundNoiseSettings.NoiseScale,
                    noiseOctaves: backgroundNoiseSettings.NoiseOctaves,
                    noiseLacunarity: backgroundNoiseSettings.NoiseLacunarity,
                    noiseGain: backgroundNoiseSettings.NoiseGain,
                    noiseAmplitude: backgroundNoiseSettings.NoiseAmplitude),
                _lifetime.Token);
            // Assign the coordinator to all tiles so lazy generation is
            // bounded and cancellable via the coordinator.
            for (var i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].Coordinator = _tileCoordinator;
                _tiles[i].ClaimantIdProvider = null; // Use per-tile claimant identity
                _tiles[i].ClaimantTokenProvider = () => _frameTileCts?.Token ?? CancellationToken.None;
                _tiles[i].ReleaseReservedCacheEntry = _tileCacheBudget.Release;
            }

            // Cache tile bounds by id for center-distance scheduling (ICW-205).
            // The map is rebuilt once per scene, not per frame.
            var tileBoundsById = new Dictionary<string, SpatialBounds>(_tiles.Count, StringComparer.Ordinal);
            for (var i = 0; i < _tiles.Count; i++)
            {
                tileBoundsById[_tiles[i].Id] = _tiles[i].Bounds;
            }

            _tileBoundsById = tileBoundsById;

            SubscribeTileGenerationEvents(_tiles);
            _sceneBounds = GetSceneBounds(_tiles);
            CanvasSurface.SetSceneBounds(_sceneBounds);

            _annotations = _tiles.SelectMany(tile => tile.Annotations).ToArray();
            _spatialIndex.AddRange(_annotations);
            UpdateSelectedAnnotationFeatures();
            StatusText.Text = $"Publishing {_annotations.Count:N0} classified annotations";
            await _spatialIndex.PublishSnapshotAsync(_lifetime.Token);

            if (fitToWidth)
            {
                FitSceneToWidth();
            }
            else
            {
                ClampCameraToScene();
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
            await RequestRenderAsync();
            // Notify scene-source consumers that the scene content changed
            // (ICW-312). The canvas and any external host re-query through
            // the source contract.
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            RegenerateButton.IsEnabled = true;
            EndBusyOperation();
            _generationGate.Release();
        }
    }

    private void SubscribeTileGenerationEvents(IReadOnlyList<SampleImageTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].PixelsGenerated += OnTilePixelsGenerated;
            tiles[i].PixelsGenerationFailed += OnTilePixelsGenerationFailed;
        }
    }

    private void UnsubscribeTileGenerationEvents(IReadOnlyList<SampleImageTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].PixelsGenerated -= OnTilePixelsGenerated;
            tiles[i].PixelsGenerationFailed -= OnTilePixelsGenerationFailed;
        }
    }

    private void OnTilePixelsGenerated(object? sender, EventArgs e)
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (!IsLoaded || _lifetime.IsCancellationRequested)
            {
                return;
            }

            await RequestRenderAsync();
        });
    }

    private void OnTilePixelsGenerationFailed(object? sender, EventArgs e)
    {
        // Trigger a re-render so the pipeline can retry generation for tiles
        // that failed. Without this, a generation failure would silently end
        // the render loop if no other event triggers a frame.
        if (!_lifetime.IsCancellationRequested)
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (!IsLoaded || _lifetime.IsCancellationRequested) return;
                await RequestRenderAsync();
            });
        }
    }

    private async Task RequestRenderAsync()
    {
        BeginBusyOperation();
        try
        {
            await _renderAction.RequestAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            EndBusyOperation();
        }
    }

    private Task DispatchRenderFrameAsync(CancellationToken cancellationToken)
    {
        if (Dispatcher.CheckAccess())
        {
            return RenderFrameAsync(cancellationToken);
        }

        return Dispatcher.InvokeAsync(() => RenderFrameAsync(cancellationToken)).Task.Unwrap();
    }

    private static void OnRenderActionFaulted(Exception exception)
    {
        Serilog.Log.Error(exception, "Render action failed");
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        if (!IsLoaded || ViewportHost.ActualWidth < 1 || ViewportHost.ActualHeight < 1)
        {
            return;
        }

        var width = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualWidth), 1, 4096);
        var height = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualHeight), 1, 4096);
        CanvasSurface.ViewModel.ApplyZoomFloor(width, height);
        _camera.ClampToBounds(_sceneBounds, width, height);
        var camera = _camera.Capture();
        var viewport = camera.GetViewportBounds(width, height);
        var stopwatch = Stopwatch.StartNew();

        // Replace the per-frame tile-work cancellation token source.
        // The previous frame's CTS is cancelled so that its tile claimants
        // are automatically removed from the coordinator. The cancelled CTS
        // is disposed on the next frame replacement to avoid disposing
        // while in-flight registrations are still running.
        var previousCts = Interlocked.Exchange(ref _frameTileCts, new CancellationTokenSource());
        previousCts?.Cancel();

        // Dispose the previous frame's CTS that was cancelled two frames ago.
        // This gives in-flight cancellation callbacks time to complete.
        if (_previousFrameTileCts is not null)
        {
            _previousFrameTileCts.Dispose();
        }
        _previousFrameTileCts = previousCts;

        // Compute the viewport interest set for tile work culling (ICW-143)
        // and center-distance scheduling (ICW-205). Visible tiles are those
        // intersecting the current viewport. Prefetch is empty for now — a
        // configurable margin can be added later.
        var mipLevel = BackgroundTileMipPolicy.SelectMipLevel(camera);
        var centerX = viewport.X + (viewport.Width / 2.0);
        var centerY = viewport.Y + (viewport.Height / 2.0);
        var visibleTileKeys = new HashSet<BackgroundTileCacheKey>();
        for (var i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].Bounds.Intersects(viewport))
            {
                var epoch = _tiles[i].CurrentGenerationEpoch;
                visibleTileKeys.Add(new BackgroundTileCacheKey("synthetic", _tiles[i].Id, epoch, mipLevel));
            }
        }

        // Squared distance from the camera center to each tile center, so the
        // coordinator drains closest visible tiles first. Returns 0 for keys
        // with no known bounds (stale revisions) to keep ordering stable.
        Func<BackgroundTileCacheKey, double>? squaredDistanceFromCenter = key =>
        {
            if (!_tileBoundsById.TryGetValue(key.TileId, out var bounds))
            {
                return 0d;
            }

            var dx = (bounds.X + (bounds.Width / 2.0)) - centerX;
            var dy = (bounds.Y + (bounds.Height / 2.0)) - centerY;
            return (dx * dx) + (dy * dy);
        };

        // Publish the interest set to the coordinator. This cancels any
        // queued generation for tiles outside the viewport and orders the
        // remaining queue by visibility, center distance, and mip suitability.
        _tileCoordinator.PublishInterestSet(new ViewportInterestSet(
            visibleTileKeys,
            new HashSet<BackgroundTileCacheKey>(),
            centerX,
            centerY,
            mipLevel,
            squaredDistanceFromCenter));

        // Track this render request for stale-frame rejection.
        var requestVersion = _renderRequestTracker.BeginRequest();

        var factory = AcquireBackBuffer(width, height);
        var frame = await Task.Run(() =>
        {
            var visibleItems = _spatialIndex.Query(viewport);
            var visibleTiles = _tiles.Where(tile => tile.Bounds.Intersects(viewport)).ToArray();
            _tileCacheBudget.SetPinnedTiles(visibleTiles);
            var bitmap = factory.GenerateFrozenBitmap(
                visibleTiles,
                visibleItems,
                camera,
                _tileCacheBudget.TryReserve,
                showBackgroundImages: _showBackgroundImages,
                showSparseImageTiles: _showImageTiles);
            return (Bitmap: bitmap, VisibleItems: visibleItems, VisibleTiles: visibleTiles);
        }, cancellationToken);

        // If a newer render request has started since we began, discard
        // this stale frame to prevent out-of-order publication.
        if (!_renderRequestTracker.IsCurrent(requestVersion))
            return;

        PublishFrame(factory, frame.Bitmap, frame.VisibleItems, camera, width, height);
        _renderRequestTracker.Advance();

        stopwatch.Stop();
        var generatedTileCount = _tiles.Count(tile => tile.IsBackgroundFetched);
        var visibleBackgroundTileCount = frame.VisibleTiles.Count(tile => tile.IsImageGenerated);
        UpdateCacheStatus(visibleBackgroundTileCount);
        var queuedTileCount = _tiles.Count(tile => tile.IsGenerationQueued);
        var completedTiles = _tiles.Where(tile => tile.GenerationDuration.HasValue).ToArray();
        var averageGenerationMilliseconds = completedTiles.Length == 0
            ? 0
            : completedTiles.Average(tile => tile.GenerationDuration!.Value.TotalMilliseconds);
        var completedConversionTiles = completedTiles
            .Where(tile => tile.BitmapConversionDuration.HasValue)
            .ToArray();
        var averageConversionMilliseconds = completedConversionTiles.Length == 0
            ? 0
            : completedConversionTiles.Average(tile => tile.BitmapConversionDuration!.Value.TotalMilliseconds);
        var coordinatorCounters = _tileCoordinator.GetCounters();

        // Update loading indicator: show RenderBusyBar when tile generation
        // is actively running or queued.
        var hasPendingTileWork = coordinatorCounters.PendingCount > 0;
        if (hasPendingTileWork && RenderBusyBar.Visibility != Visibility.Visible)
        {
            RenderBusyBar.Visibility = Visibility.Visible;
        }
        else if (!hasPendingTileWork && RenderBusyBar.Visibility == Visibility.Visible)
        {
            RenderBusyBar.Visibility = Visibility.Collapsed;
        }

        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  " +
            $"Zoom {camera.ScaleX:F3}x  |  " +
            $"Backgrounds {visibleBackgroundTileCount}/{frame.VisibleTiles.Length} visible, {generatedTileCount} total  |  " +
            $"Queue {queuedTileCount}  |  Gen {averageGenerationMilliseconds:F1} ms  |  " +
            $"Coord {{A{coordinatorCounters.ActiveCount} Q{coordinatorCounters.QueuedCount} " +
            $"C{coordinatorCounters.CompletedCount} X{coordinatorCounters.CanceledCount} " +
            $"F{coordinatorCounters.FailedCount}}}";
        UpdateZoomDisplay(camera, width, height);
        // Accumulate frame timing for periodic diagnostics logging.
        var frameElapsed = stopwatch.Elapsed;
        _lastFrameTicks = frameElapsed.Ticks;
        _totalFrameTicks += frameElapsed.Ticks;
        _frameCount++;
        _diagnosticsFrameCount++;

        // Log diagnostics every 120 frames (~2 seconds at 60 fps).
        if (_diagnosticsFrameCount >= 120 && _diagnosticsStopwatch.Elapsed.TotalSeconds >= 1.0)
        {
            var avgMs = _totalFrameTicks / (double)_frameCount / TimeSpan.TicksPerMillisecond;
            var totalTiles = _tiles.Count;
            var fetchedTiles = _tiles.Count(t => t.IsBackgroundFetched);
            var generatedMs = completedTiles.Length == 0
                ? 0.0
                : completedTiles.Average(t => t.GenerationDuration?.TotalMilliseconds ?? 0);

            Serilog.Log.Information(
                "FrameDiag: {FrameCount,6}f | avg {AvgMs,7:F1}ms | " +
                "coord {CoordActive,3}a/{CoordQueued,3}q/{CoordCompleted,6}c/{CoordCanceled,4}x/{CoordFailed,3}f | " +
                "tiles {FetchedTiles,4}/{TotalTiles,4} fetched | " +
                "avgGen {AvgGenMs,6:F1}ms | " +
                "budget {BudgetBytes,10:N0}b",
                _frameCount, avgMs,
                coordinatorCounters.ActiveCount, coordinatorCounters.QueuedCount,
                coordinatorCounters.CompletedCount, coordinatorCounters.CanceledCount,
                coordinatorCounters.FailedCount,
                fetchedTiles, totalTiles, generatedMs,
                _tileCacheBudget.MaxBytes);

            _diagnosticsFrameCount = 0;
            _diagnosticsStopwatch.Restart();
        }

        if (_hoverPointerPosition is Point hoverPointer)
        {
            UpdatePixelometer(hoverPointer);
        }
    }

    private ZeroCopyBitmapFactory AcquireBackBuffer(int width, int height)
    {
        return _frameBufferPool.AcquireBackBuffer(width, height);
    }

    private void PublishFrame(
        ZeroCopyBitmapFactory renderedBuffer,
        ImageSource bitmap,
        IReadOnlyList<SampleAnnotation> annotations,
        CameraSnapshot camera,
        int frameWidth,
        int frameHeight)
    {
        _lastPublishedCamera = camera;
        // The canvas consumes a CanvasFrame value, never a host-built UIElement
        // tree (ICW-315, ADR-0007). The raster handoff is zero-copy: the canvas
        // displays the frozen ImageSource and never touches its backing memory
        // section. IReadOnlyList<out T> covariance carries the SampleAnnotation
        // list through the ICanvasItem contract without a mapping.
        var frame = new CanvasFrame(
            raster: bitmap,
            items: annotations,
            viewport: camera.GetViewportBounds(frameWidth, frameHeight),
            visibleItemCount: annotations.Count,
            totalItemCount: _spatialIndex.Count,
            width: frameWidth,
            height: frameHeight);
        CanvasSurface.PublishFrame(frame);
        // Triple-buffer rotation (ICW-P0-BUFFER-REUSE-SYNC). The buffer that
        // was displayed until now moves to the retired slot instead of being
        // recycled as the back buffer immediately, so WPF's compositor has a
        // full frame cycle to finish reading it before it is rewritten.
        _frameBufferPool.Publish(renderedBuffer);
    }

    private void OnCanvasFramePublished(object? sender, CanvasFrame frame)
    {
        // Host-composed overlays stay camera-synchronized with the raster that
        // was published for this frame (ICW-315; overlay layering invariant).
        UpdateTileGridLayer(_lastPublishedCamera, frame.Width, frame.Height);
        UpdateAnnotationLayer(frame.Items, _lastPublishedCamera);
    }

    private void UpdateTileGridLayer(CameraSnapshot camera, int frameWidth, int frameHeight)
    {
        var gridLayer = CanvasSurface.TileGridLayer!;
        gridLayer.Children.Clear();
        var gridBrush = new SolidColorBrush(Color.FromArgb(180, 40, 210, 190));
        gridBrush.Freeze();

        foreach (var worldX in _tiles.SelectMany(tile => new[] { tile.Bounds.X, tile.Bounds.Right }).Distinct())
        {
            var screenX = camera.WorldToScreen(worldX, _sceneBounds.Y).X;
            gridLayer.Children.Add(new Line
            {
                X1 = screenX,
                X2 = screenX,
                Y1 = 0,
                Y2 = frameHeight,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        foreach (var worldY in _tiles.SelectMany(tile => new[] { tile.Bounds.Y, tile.Bounds.Bottom }).Distinct())
        {
            var screenY = camera.WorldToScreen(_sceneBounds.X, worldY).Y;
            gridLayer.Children.Add(new Line
            {
                X1 = 0,
                X2 = frameWidth,
                Y1 = screenY,
                Y2 = screenY,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }
    }

    private void UpdateAnnotationLayer(IReadOnlyList<ICanvasItem> items, CameraSnapshot camera)
    {
        var annotationLayer = CanvasSurface.AnnotationLayer!;
        annotationLayer.Children.Clear();

        foreach (var item in items)
        {
            // The host composes app-specific visuals. ICW-314 moves selection
            // and tooltip ownership into the canvas against the item contract.
            if (item is not SampleAnnotation annotation)
            {
                continue;
            }

            var topLeft = camera.WorldToScreen(annotation.Bounds.X, annotation.Bounds.Y);
            var width = annotation.Bounds.Width * camera.ScaleX;
            var height = annotation.Bounds.Height * camera.ScaleY;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var outlineBrush = new SolidColorBrush(ToMediaColor(annotation.Color));
            var fillBrush = CreateFillBrush(_annotationDisplayOptions.Mode, annotation.Color);
            var outline = new Rectangle
            {
                Stroke = outlineBrush,
                Fill = fillBrush,
                StrokeThickness = _annotationDisplayOptions.OutlineThickness,
                SnapsToDevicePixels = true,
                StrokeDashCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            var annotationVisual = new Grid
            {
                Children = { outline }
            };
            var annotationElement = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent,
                Child = annotationVisual,
                Tag = annotation,
                ToolTip = new DeferredAnnotationToolTip(annotation)
            };
            annotationElement.MouseLeftButtonDown += OnAnnotationMouseLeftButtonDown;
            Canvas.SetLeft(annotationElement, topLeft.X);
            Canvas.SetTop(annotationElement, topLeft.Y);
            annotationLayer.Children.Add(annotationElement);

            if (_annotationDisplayOptions.ShowLabels)
            {
                var labelPanel = BuildAnnotationLabel(
                    annotation,
                    topLeft,
                    outlineBrush,
                    _annotationDisplayOptions.LabelSize,
                    _annotationDisplayOptions.LabelDisplay);
                annotationLayer.Children.Add(labelPanel);
            }

            if (annotation.Id == _selectedAnnotationId)
            {
                _selectionOutlineAnimator.Apply(outline);
            }
        }
    }

    private void FitSceneToWidth()
    {
        ApplyFitToWidthZoom();
        ClampCameraToScene();
    }

    private static Border BuildAnnotationLabel(
        SampleAnnotation annotation,
        ScreenPoint topLeft,
        SolidColorBrush outlineBrush,
        double labelSize,
        AnnotationLabelDisplay labelDisplay)
    {
        var labelPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 16, 22, 28)),
            BorderBrush = outlineBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = labelDisplay == AnnotationLabelDisplay.Id
                    ? annotation.ObjectId
                    : annotation.Classification,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono"),
                FontWeight = FontWeights.SemiBold,
                FontSize = labelSize,
                TextAlignment = TextAlignment.Left
            }
        };

        Canvas.SetLeft(labelPanel, topLeft.X);
        Canvas.SetTop(labelPanel, topLeft.Y - 22);
        return labelPanel;
    }

    private static Brush CreateFillBrush(AnnotationDisplayMode mode, Bgra32Color classColor)
    {
        var fillColor = Color.FromArgb(220, classColor.Red, classColor.Green, classColor.Blue);
        var overlayColor = Color.FromArgb(64, classColor.Red, classColor.Green, classColor.Blue);
        return mode switch
        {
            AnnotationDisplayMode.Outline => Brushes.Transparent,
            AnnotationDisplayMode.Fill => new SolidColorBrush(fillColor),
            AnnotationDisplayMode.OutlineAndFill => new SolidColorBrush(overlayColor),
            _ => Brushes.Transparent
        };
    }

    private static Color ToMediaColor(Bgra32Color color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    private static SpatialBounds GetSceneBounds(IReadOnlyList<SampleImageTile> tiles)
    {
        var left = tiles.Min(tile => tile.Bounds.X);
        var top = tiles.Min(tile => tile.Bounds.Y);
        var right = tiles.Max(tile => tile.Bounds.Right);
        var bottom = tiles.Max(tile => tile.Bounds.Bottom);
        return new SpatialBounds(left, top, right - left, bottom - top);
    }

    private void ClampCameraToScene()
    {
        var width = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualWidth), 1, 4096);
        var height = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualHeight), 1, 4096);
        CanvasSurface.ViewModel.ApplyZoomFloor(width, height);
        _camera.ClampToBounds(_sceneBounds, width, height);
        CanvasSurface.RefreshScrollbars();
    }

    private async void OnAnnotationMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: SampleAnnotation annotation })
        {
            _selectedAnnotationId = annotation.Id;
            UpdateSelectedAnnotationFeatures(annotation);
            e.Handled = true;
            await RequestRenderAsync();
        }
    }

    private async void OnShowBackgroundImagesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showBackgroundImages = ShowBackgroundImagesCheckBox.IsChecked ?? true;
        CanvasSurface.RasterVisible = _showBackgroundImages || _showImageTiles;
        await RequestRenderAsync();
    }

    private async void OnShowImageTilesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showImageTiles = ShowImageTilesCheckBox.IsChecked ?? true;
        CanvasSurface.RasterVisible = _showBackgroundImages || _showImageTiles;
        await RequestRenderAsync();
    }

    private void OnCanvasPointerWheel(object? sender, MouseWheelEventArgs e)
    {
        // Wheel zoom is handled by the canvas control (ICW-311). The window
        // only observes the pointer for its pixelometer readout.
        var origin = e.GetPosition(ViewportHost);
        _hoverPointerPosition = origin;
        UpdatePixelometer(origin);
    }

    private void UpdateZoomDisplay(CameraSnapshot camera, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = CanvasSurface.ViewModel.ComputeMinimumZoom(viewportWidth, viewportHeight);
        var percent = ViewportZoomPolicy.ComputeDisplayPercent(
            camera.ScaleX,
            camera.ScaleY,
            minimumScaleX,
            minimumScaleY);
        ZoomPresetComboBox.Text = $"{percent:F0}%";
    }

    private async void OnZoomPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var mode = ZoomPresetComboBox.SelectedIndex;
        if (mode < 0)
        {
            return;
        }

        ZoomPresetComboBox.SelectedIndex = -1;
        if (mode == 7)
        {
            CustomZoomPanel.Visibility = Visibility.Visible;
            CustomZoomTextBox.Focus();
            CustomZoomTextBox.SelectAll();
            return;
        }

        CustomZoomPanel.Visibility = Visibility.Collapsed;
        await ApplyZoomPresetAsync(mode);
    }

    private async void OnCustomZoomClicked(object sender, RoutedEventArgs e)
    {
        await ApplyCustomZoomAsync();
    }

    private async void OnCustomZoomKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyCustomZoomAsync();
    }

    private async Task ApplyCustomZoomAsync()
    {
        if (!double.TryParse(CustomZoomTextBox.Text, out var percent)
            || !double.IsFinite(percent)
            || percent <= 0)
        {
            StatusText.Text = "Custom zoom must be a positive percentage";
            CustomZoomTextBox.Focus();
            CustomZoomTextBox.SelectAll();
            return;
        }

        ApplyPercentZoom(percent);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private async Task ApplyZoomPresetAsync(int mode)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        switch (mode)
        {
            case 0:
                ApplyFitToWidthZoom();
                break;
            case 1:
                ApplyFitToHeightZoom();
                break;
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
                var percent = mode switch
                {
                    2 => 50,
                    3 => 75,
                    4 => 100,
                    5 => 150,
                    _ => 200
                };
                ApplyPercentZoom(percent);
                break;
            default:
                return;
        }

        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void ApplyPercentZoom(double percent)
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = CanvasSurface.ViewModel.ComputeMinimumZoom(width, height);
        var baseUniformScale = Math.Max(minimumScaleX, minimumScaleY);
        var targetScale = baseUniformScale * (percent / 100.0);
        var delta = targetScale / _camera.ScaleX;

        _camera.Zoom(delta, delta, new ScreenPoint(width / 2, height / 2));
    }

    private void ApplyFitToWidthZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = CanvasSurface.ViewModel.ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleX, minimumScaleY, width, height);
    }

    private void ApplyFitToHeightZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = CanvasSurface.ViewModel.ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleY, minimumScaleY, width, height);
    }

    private void ApplyScaleWithUniformFirst(double preferredUniformScale, double fallbackScaleY, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = CanvasSurface.ViewModel.ComputeMinimumZoom(viewportWidth, viewportHeight);
        var minimumUniform = Math.Max(minimumScaleX, minimumScaleY);

        if (preferredUniformScale >= minimumUniform)
        {
            var uniformDelta = preferredUniformScale / _camera.ScaleX;
            _camera.Zoom(uniformDelta, uniformDelta, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
            return;
        }

        var targetScaleX = Math.Max(minimumScaleX, preferredUniformScale);
        var targetScaleY = Math.Max(minimumScaleY, fallbackScaleY);
        var deltaX = targetScaleX / _camera.ScaleX;
        var deltaY = targetScaleY / _camera.ScaleY;
        _camera.Zoom(deltaX, deltaY, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || LoadingOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private async void OnResizeElapsed(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        try
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnDisplayModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnOutlineThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnLabelSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnLabelDisplaySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnShowLabelsChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private void ApplyDisplayOptionsFromUi()
    {
        var selectedMode = DisplayModeComboBox.SelectedIndex switch
        {
            1 => AnnotationDisplayMode.Fill,
            2 => AnnotationDisplayMode.OutlineAndFill,
            _ => AnnotationDisplayMode.Outline
        };

        _annotationDisplayOptions = new AnnotationDisplayOptions(
            selectedMode,
            Math.Round(OutlineThicknessSlider.Value, 2),
            Math.Round(LabelSizeSlider.Value, 2),
            LabelDisplayComboBox.SelectedIndex == 1 ? AnnotationLabelDisplay.Id : AnnotationLabelDisplay.Class,
            ShowLabelsCheckBox.IsChecked ?? true);
    }

    private async void OnRegenerateClicked(object sender, RoutedEventArgs e)
    {
        if (!TryReadGenerationOptions(out var validationError))
        {
            StatusText.Text = validationError;
            return;
        }

        _selectedAnnotationId = null;
        UpdateSelectedAnnotationFeatures();
        await RegenerateSceneAsync(fitToWidth: true);
    }

    private async void OnDebugDumpCacheClicked(object sender, RoutedEventArgs e)
    {
        var fetchedTiles = _tiles.Where(tile => tile.IsBackgroundFetched).Select(tile => tile.Id).ToArray();
        var dump = $"Cache fetched {fetchedTiles.Length}/{_tiles.Count}: {string.Join(", ", fetchedTiles.Take(10))}{(fetchedTiles.Length > 10 ? "..." : string.Empty)}";
        Serilog.Log.Debug(dump);

        foreach (var tile in _tiles)
        {
            tile.ResetImageCache();
        }

        _tileCacheBudget = new TileCacheBudget(_tileCacheBudget.MaxBytes);
        UpdateCacheStatus();
        StatusText.Text = "Image cache reset. Tiles will regenerate lazily as they come into range.";
        await RequestRenderAsync();
    }

    private void UpdateCacheStatus(int? visibleBackgroundTileCount = null)
    {
        var visibleCount = visibleBackgroundTileCount ?? 0;
        var cacheSummary = _tileCacheBudget.DescribeStatus();
        CacheStatusText.Text = cacheSummary;
        Serilog.Log.Debug("Cache summary: {Summary} (visible backgrounds {Visible})", cacheSummary, visibleCount);
    }

    private void UpdateSelectedAnnotationFeatures(SampleAnnotation? annotation = null)
    {
        var selectedAnnotation = annotation ?? _annotations.FirstOrDefault(item => item.Id == _selectedAnnotationId);
        if (selectedAnnotation is null)
        {
            _selectedAnnotationFeatures = [];
            FeatureDataGrid.ItemsSource = SelectedAnnotationFeatures;
            return;
        }

        _selectedAnnotationFeatures = selectedAnnotation.GetFeatureDisplayItems();
        FeatureDataGrid.ItemsSource = SelectedAnnotationFeatures;
    }

    private bool TryReadGenerationOptions(out string validationError)
    {
        validationError = string.Empty;

        // The SliderTextBox controls clamp each value to its configured range,
        // so only the cross-field tile-count cap needs explicit validation here.
        var columns = (int)Math.Round(TilesXSliderTextBox.Value);
        var rows = (int)Math.Round(TilesYSliderTextBox.Value);
        var objectsPerTile = (int)Math.Round(ObjectsPerTileSliderTextBox.Value);
        var seed = (int)Math.Round(GenerationSeedSliderTextBox.Value);

        if (columns <= 0 || rows <= 0)
        {
            validationError = "Tiles X and Tiles Y must be positive integers.";
            return false;
        }

        if ((long)columns * rows > 2000)
        {
            validationError = "Tile count must be 2000 or less for this demo.";
            return false;
        }

        _tileColumns = columns;
        _tileRows = rows;
        _objectsPerTile = objectsPerTile;
        _generationSeed = seed;
        return true;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        SaveSettings();
        _resizeTimer.Stop();
        UnsubscribeTileGenerationEvents(_tiles);
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        CanvasSurface.DetachFrameShell();
        _frameBufferPool.Dispose();
        _tileCoordinator.CancelAll();
        _tileCoordinator.Dispose();
        _generationGate.Dispose();
        _lifetime.Dispose();
    }

    private void SaveSettings()
    {
        var settings = new CanvasUserSettings
        {
            TileColumns = (int)Math.Round(TilesXSliderTextBox.Value),
            TileRows = (int)Math.Round(TilesYSliderTextBox.Value),
            ObjectsPerTile = (int)Math.Round(ObjectsPerTileSliderTextBox.Value),
            GenerationSeed = (int)Math.Round(GenerationSeedSliderTextBox.Value),
            AnnotationDisplayMode = DisplayModeComboBox.SelectedIndex,
            OutlineThickness = OutlineThicknessSlider.Value,
            LabelSize = LabelSizeSlider.Value,
            LabelDisplay = LabelDisplayComboBox.SelectedIndex,
            ShowLabels = ShowLabelsCheckBox.IsChecked ?? true,
            ShowImageTiles = _showImageTiles,
            ShowBackgroundImages = _showBackgroundImages,
            BackgroundTargetValue = (byte)Math.Round(_mainViewModel.TileBackgroundNoiseSettings.TargetValue),
            BackgroundNoise = (byte)Math.Round(_mainViewModel.TileBackgroundNoiseSettings.Noise),
            BackgroundCircleCount = (int)Math.Round(_mainViewModel.TileBackgroundNoiseSettings.CircleCount),
            BackgroundNoiseScale = _mainViewModel.TileBackgroundNoiseSettings.Scale,
            BackgroundNoiseOctaves = (int)Math.Round(_mainViewModel.TileBackgroundNoiseSettings.Octaves),
            BackgroundNoiseLacunarity = _mainViewModel.TileBackgroundNoiseSettings.Lacunarity,
            BackgroundNoiseGain = _mainViewModel.TileBackgroundNoiseSettings.Gain,
            BackgroundNoiseAmplitude = _mainViewModel.TileBackgroundNoiseSettings.Amplitude
        };

        if (!settings.IsValid)
        {
            settings = settings with
            {
                TileColumns = _tileColumns,
                TileRows = _tileRows,
                ObjectsPerTile = _objectsPerTile
            };
        }

        try
        {
            CanvasUserSettingsStore.Save(_settingsPath, settings);
        }
        catch (System.IO.IOException exception)
        {
            Serilog.Log.Warning(exception, "Unable to save canvas settings to {Path}", _settingsPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            Serilog.Log.Warning(exception, "Unable to save canvas settings to {Path}", _settingsPath);
        }
    }

    private void UpdatePixelometer(Point screenPoint)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        var camera = _camera.Capture();
        var worldX = (screenPoint.X - camera.OffsetX) / camera.ScaleX;
        var worldY = (screenPoint.Y - camera.OffsetY) / camera.ScaleY;

        PixelometerWorldText.Text = $"WORLD X {worldX:F1}  Y {worldY:F1}";

        var mipLevel = BackgroundTileMipPolicy.SelectMipLevel(camera);
        // Read through the scene source contract so the read never initiates
        // tile generation (ICW-312 closes ICW-P0-PIXELOMETER-READOUT). When
        // another host supplies a different source, the pixelometer works
        // against the same non-blocking contract.
        var sceneSource = CanvasSurface.SceneSource;
        if (sceneSource is not null
            && sceneSource.TryReadResidentPixel(worldX, worldY, mipLevel, out var sample))
        {
            var finalValue = ResolveDisplayPixelValue(sample.Background, worldX, worldY);
            PixelometerTileText.Text = sample.TileInfo;
            PixelometerValueText.Text = $"PIXEL {finalValue}  ({sample.TileId}) bg {sample.Background} + defect {sample.Defect}";
            return;
        }

        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private byte ResolveDisplayPixelValue(byte backgroundValue, double worldX, double worldY)
    {
        var sampleArea = new SpatialBounds(worldX, worldY, 0.01, 0.01);
        var hitAnnotations = _spatialIndex.Query(sampleArea);
        return DefectOverlaySampler.ResolveDisplayValue(backgroundValue, hitAnnotations, worldX, worldY);
    }

    private void BeginBusyOperation()
    {
        if (Interlocked.Increment(ref _busyOperationCount) == 1)
        {
            Dispatcher.Invoke(() => RenderBusyBar.Visibility = Visibility.Visible);
        }
    }

    private void EndBusyOperation()
    {
        if (Interlocked.Decrement(ref _busyOperationCount) <= 0)
        {
            Interlocked.Exchange(ref _busyOperationCount, 0);
            Dispatcher.Invoke(() => RenderBusyBar.Visibility = Visibility.Collapsed);
        }
    }

    private sealed record AnnotationDisplayOptions(
        AnnotationDisplayMode Mode,
        double OutlineThickness,
        double LabelSize,
        AnnotationLabelDisplay LabelDisplay,
        bool ShowLabels)
    {
        public static AnnotationDisplayOptions Default { get; } = new(
            AnnotationDisplayMode.Outline,
            2,
                8.5,
                AnnotationLabelDisplay.Class,
            true);
    }

    private enum AnnotationLabelDisplay
    {
        Class,
        Id
    }

    private enum AnnotationDisplayMode
    {
        Outline,
        Fill,
        OutlineAndFill
    }

    private interface ISelectionOutlineAnimator
    {
        void Apply(Shape outline);
    }

    private sealed class MarchingDashSelectionOutlineAnimator : ISelectionOutlineAnimator
    {
        public void Apply(Shape outline)
        {
            outline.StrokeDashArray = [4, 3];
            var animation = new DoubleAnimation(0, -14, TimeSpan.FromMilliseconds(420))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            outline.BeginAnimation(Shape.StrokeDashOffsetProperty, animation);
        }
    }

    private sealed class PulseOpacitySelectionOutlineAnimator : ISelectionOutlineAnimator
    {
        public void Apply(Shape outline)
        {
            var animation = new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(360))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            outline.BeginAnimation(OpacityProperty, animation);
        }
    }

    private enum SelectionOutlineAnimationMode
    {
        MarchingDash,
        PulseOpacity
    }

    private static class SelectionOutlineAnimatorFactory
    {
        public static ISelectionOutlineAnimator Create(SelectionOutlineAnimationMode mode)
        {
            return mode switch
            {
                SelectionOutlineAnimationMode.PulseOpacity => new PulseOpacitySelectionOutlineAnimator(),
                _ => new MarchingDashSelectionOutlineAnimator()
            };
        }
    }
}
