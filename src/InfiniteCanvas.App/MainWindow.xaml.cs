using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;
using Serilog;

namespace InfiniteCanvas.App;

public partial class MainWindow : Window
{
    private LiveSpatialIndexService<SampleAnnotation> _spatialIndex = null!;
    private CanvasViewportViewModel<SampleAnnotation> _viewModel = null!;
    private CameraTransform _camera = null!;
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _resizeTimer;
    private readonly DispatcherTimer _anchorPanTimer;
    private readonly ISelectionOutlineAnimator _selectionOutlineAnimator;
    private MainViewModel _mainViewModel = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _generationGate = new(1, 1);
    private readonly string _settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InfiniteCanvas",
        "settings.json");
    private IReadOnlyList<SampleImageTile> _tiles = [];
    private IReadOnlyList<SampleAnnotation> _annotations = [];
    private SpatialBounds _sceneBounds;
    private ZeroCopyBitmapFactory? _frontBitmapFactory;
    private ZeroCopyBitmapFactory? _backBitmapFactory;
    private Point? _lastPointerPosition;
    private Point? _anchorPanOrigin;
    private Point _anchorPanPointer;
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
    private ViewportScrollbarAxis? _scrollbarDragAxis;
    private double _scrollbarDragPointerOffset;
    private Canvas? _viewportScrollbarOverlay;
    private Border? _horizontalScrollbarTrack;
    private Border? _horizontalScrollbarThumb;
    private Border? _verticalScrollbarTrack;
    private Border? _verticalScrollbarThumb;
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
    private Viewbox FramePresenter => CanvasSurface.FrameHost;
    private Canvas ViewportScrollbarOverlay => CanvasSurface.ScrollbarHost;
    private Border HorizontalScrollbarTrack => CanvasSurface.HorizontalTrack;
    private Border HorizontalScrollbarThumb => CanvasSurface.HorizontalThumb;
    private Border VerticalScrollbarTrack => CanvasSurface.VerticalTrack;
    private Border VerticalScrollbarThumb => CanvasSurface.VerticalThumb;
    private Ellipse PanAnchorVisual => CanvasSurface.AnchorVisual;
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
        CanvasSurface.PointerWheel += OnViewportMouseWheel;
        CanvasSurface.SizeChanged += OnViewportSizeChanged;

        _viewportScrollbarOverlay = CanvasSurface.ViewportScrollbarOverlay;
        _horizontalScrollbarTrack = CanvasSurface.HorizontalScrollbarTrack;
        _horizontalScrollbarThumb = CanvasSurface.HorizontalScrollbarThumb;
        _verticalScrollbarTrack = CanvasSurface.VerticalScrollbarTrack;
        _verticalScrollbarThumb = CanvasSurface.VerticalScrollbarThumb;

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
        _anchorPanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, OnAnchorPanTick, Dispatcher)
        {
            IsEnabled = false
        };

        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
        ApplySettingsToUi(CanvasUserSettingsStore.Load(_settingsPath));

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void InitializeSpatialState()
    {
        _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(new StrTreeSpatialIndexBuilder<SampleAnnotation>());
        _viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);
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
        EnforceZoomFloor(width, height);
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

        // Compute the viewport interest set for tile work culling (ICW-143).
        // Visible tiles are those intersecting the current viewport. Prefetch
        // is empty for now — a configurable margin can be added later.
        var mipLevel = BackgroundTileMipPolicy.SelectMipLevel(camera);
        var visibleTileKeys = new HashSet<BackgroundTileCacheKey>();
        for (var i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].Bounds.Intersects(viewport))
            {
                var epoch = _tiles[i].CurrentGenerationEpoch;
                visibleTileKeys.Add(new BackgroundTileCacheKey("synthetic", _tiles[i].Id, epoch, mipLevel));
            }
        }

        // Publish the interest set to the coordinator. This cancels any
        // queued or running generation for tiles outside the viewport.
        _tileCoordinator.PublishInterestSet(new ViewportInterestSet(visibleTileKeys, new HashSet<BackgroundTileCacheKey>()));

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

        var frameVisual = BuildFrameVisual(frame.Bitmap, frame.VisibleItems, camera, width, height);
        PublishFrame(factory, frameVisual);
        _renderRequestTracker.Advance();
        _viewModel.ApplyFrame(viewport, frame.VisibleItems.Count);
        _mainViewModel.ApplyViewportState(frame.VisibleItems.Count, _spatialIndex.Count);

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
        if (_backBitmapFactory is not null
            && _backBitmapFactory.Width == width
            && _backBitmapFactory.Height == height)
        {
            return _backBitmapFactory;
        }

        _backBitmapFactory?.Dispose();
        _backBitmapFactory = new ZeroCopyBitmapFactory(width, height);
        return _backBitmapFactory;
    }

    private void PublishFrame(ZeroCopyBitmapFactory renderedBuffer, Grid frameVisual)
    {
        FramePresenter.Child = frameVisual;

        var previousFront = _frontBitmapFactory;
        _frontBitmapFactory = renderedBuffer;
        _backBitmapFactory = null;

        if (previousFront is not null
            && previousFront.Width == renderedBuffer.Width
            && previousFront.Height == renderedBuffer.Height)
        {
            _backBitmapFactory = previousFront;
        }
        else
        {
            previousFront?.Dispose();
        }
    }

    private void FitSceneToWidth()
    {
        ApplyFitToWidthZoom();
        ClampCameraToScene();
    }

    private Grid BuildFrameVisual(
        ImageSource bitmap,
        IReadOnlyList<SampleAnnotation> annotations,
        CameraSnapshot camera,
        int frameWidth,
        int frameHeight)
    {
        var frame = new Grid
        {
            Width = frameWidth,
            Height = frameHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        if (_showBackgroundImages || _showImageTiles)
        {
            frame.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            });
        }

        frame.Children.Add(BuildTileGridLayer(camera, frameWidth, frameHeight));

        var annotationLayer = new Canvas { ClipToBounds = true };
        frame.Children.Add(annotationLayer);

        foreach (var annotation in annotations)
        {
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

        return frame;
    }

    private Canvas BuildTileGridLayer(CameraSnapshot camera, int frameWidth, int frameHeight)
    {
        var gridLayer = new Canvas
        {
            ClipToBounds = true,
            IsHitTestVisible = false
        };
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

        return gridLayer;
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
        EnforceZoomFloor(width, height);
        _camera.ClampToBounds(_sceneBounds, width, height);
        CanvasSurface.RefreshScrollbars();
    }

    private void EnforceZoomFloor(double viewportWidth, double viewportHeight)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
        var currentScaleX = _camera.ScaleX;
        var currentScaleY = _camera.ScaleY;
        if (currentScaleX >= minimumScaleX && currentScaleY >= minimumScaleY)
        {
            return;
        }

        var minimumUniform = Math.Max(minimumScaleX, minimumScaleY);
        if (Math.Abs(currentScaleX - currentScaleY) <= 0.0001)
        {
            var uniformDelta = minimumUniform / currentScaleX;
            _camera.Zoom(uniformDelta, uniformDelta, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
            return;
        }

        var origin = new ScreenPoint(viewportWidth / 2, viewportHeight / 2);
        var scaleXDelta = currentScaleX < minimumScaleX ? minimumScaleX / currentScaleX : 1;
        var scaleYDelta = currentScaleY < minimumScaleY ? minimumScaleY / currentScaleY : 1;
        _camera.Zoom(scaleXDelta, scaleYDelta, origin);
    }

    private (double ScaleX, double ScaleY) ComputeMinimumZoom(double viewportWidth, double viewportHeight)
    {
        return (
            viewportWidth / _sceneBounds.Width,
            viewportHeight / _sceneBounds.Height);
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

    private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        _lastPointerPosition = e.GetPosition(ViewportHost);
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        _lastPointerPosition = null;
        ViewportHost.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private async void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(ViewportHost);
        _hoverPointerPosition = current;
        UpdatePixelometer(current);

        if (_anchorPanOrigin is not null)
        {
            _anchorPanPointer = current;
        }

        if (_lastPointerPosition is not Point previous || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _lastPointerPosition = current;
        _camera.Pan(current.X - previous.X, current.Y - previous.Y);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void OnViewportMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _anchorPanOrigin = e.GetPosition(ViewportHost);
        _anchorPanPointer = _anchorPanOrigin.Value;
        _lastPointerPosition = null;
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.ScrollAll;
        ShowPanAnchor(_anchorPanOrigin.Value);
        _anchorPanTimer.Start();
        e.Handled = true;
    }

    private void OnViewportMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is null)
        {
            return;
        }

        StopAnchorPan();
        e.Handled = true;
    }

    private async void OnAnchorPanTick(object? sender, EventArgs e)
    {
        if (_anchorPanOrigin is not Point anchor)
        {
            return;
        }

        const double deadZone = 6;
        const double gain = 0.12;

        var deltaX = _anchorPanPointer.X - anchor.X;
        var deltaY = _anchorPanPointer.Y - anchor.Y;
        var adjustedX = ApplyDeadZone(deltaX, deadZone);
        var adjustedY = ApplyDeadZone(deltaY, deadZone);
        if (adjustedX == 0 && adjustedY == 0)
        {
            return;
        }

        _camera.Pan(-(adjustedX * gain), -(adjustedY * gain));
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private static double ApplyDeadZone(double value, double deadZone)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= deadZone)
        {
            return 0;
        }

        return Math.Sign(value) * (magnitude - deadZone);
    }

    private void ShowPanAnchor(Point anchor)
    {
        Canvas.SetLeft(PanAnchorVisual, anchor.X - (PanAnchorVisual.Width / 2));
        Canvas.SetTop(PanAnchorVisual, anchor.Y - (PanAnchorVisual.Height / 2));
        PanAnchorVisual.Visibility = Visibility.Visible;
    }

    private void StopAnchorPan()
    {
        _anchorPanTimer.Stop();
        _anchorPanOrigin = null;
        PanAnchorVisual.Visibility = Visibility.Collapsed;
        ViewportHost.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private async void OnScrollbarTrackMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border track || e.OriginalSource is Border { Name: "HorizontalScrollbarThumb" or "VerticalScrollbarThumb" })
        {
            return;
        }

        var axis = track == _horizontalScrollbarTrack ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        if (_viewportScrollbarOverlay is null)
        {
            return;
        }

        var thumb = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarThumb : _verticalScrollbarThumb;
        if (thumb is null)
        {
            return;
        }

        var pointer = e.GetPosition(_viewportScrollbarOverlay);
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackPosition = axis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(track) : Canvas.GetTop(track);
        var target = (pointerPosition - trackPosition - (thumbLength / 2)) / Math.Max(1, trackLength - thumbLength);
        await PanToScrollbarPositionAsync(axis, target);
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border thumb)
        {
            return;
        }

        _scrollbarDragAxis = thumb == _horizontalScrollbarThumb ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        var track = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarTrack : _verticalScrollbarTrack;
        if (track is null)
        {
            return;
        }

        var pointer = e.GetPosition(track);
        var thumbPosition = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(thumb) : Canvas.GetTop(thumb);
        _scrollbarDragPointerOffset = (_scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y) - thumbPosition;
        thumb.CaptureMouse();
        e.Handled = true;
    }

    private async void OnScrollbarThumbMouseMove(object sender, MouseEventArgs e)
    {
        if (_scrollbarDragAxis is not ViewportScrollbarAxis axis || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var track = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarTrack : _verticalScrollbarTrack;
        var thumb = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarThumb : _verticalScrollbarThumb;
        if (track is null || thumb is null)
        {
            return;
        }

        var pointer = e.GetPosition(track);
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        await PanToScrollbarPositionAsync(axis, (pointerPosition - _scrollbarDragPointerOffset) / Math.Max(1, trackLength - thumbLength));
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border thumb)
        {
            thumb.ReleaseMouseCapture();
        }

        _scrollbarDragAxis = null;
        e.Handled = true;
    }

    private async Task PanToScrollbarPositionAsync(ViewportScrollbarAxis axis, double targetPosition)
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var delta = ViewportScrollbarPolicy.ComputePanDelta(_camera.Capture(), _sceneBounds, width, height, axis, targetPosition);
        if (delta == 0)
        {
            return;
        }

        _camera.Pan(axis == ViewportScrollbarAxis.Horizontal ? delta : 0, axis == ViewportScrollbarAxis.Vertical ? delta : 0);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void UpdateViewportScrollbars(CameraSnapshot camera, double viewportWidth, double viewportHeight)
    {
        if (_viewportScrollbarOverlay is null || _horizontalScrollbarTrack is null || _horizontalScrollbarThumb is null || _verticalScrollbarTrack is null || _verticalScrollbarThumb is null)
        {
            return;
        }

        const double margin = 10;
        const double thickness = 10;
        const double minimumThumbLength = 24;
        var horizontalLength = Math.Max(0, viewportWidth - (margin * 2) - thickness - 4);
        var verticalLength = Math.Max(0, viewportHeight - (margin * 2) - thickness - 4);
        UpdateScrollbar(ViewportScrollbarAxis.Horizontal, ViewportScrollbarPolicy.ComputeMetrics(camera, _sceneBounds, viewportWidth, viewportHeight, ViewportScrollbarAxis.Horizontal), _horizontalScrollbarTrack, _horizontalScrollbarThumb, margin, viewportHeight - margin - thickness, horizontalLength, minimumThumbLength);
        UpdateScrollbar(ViewportScrollbarAxis.Vertical, ViewportScrollbarPolicy.ComputeMetrics(camera, _sceneBounds, viewportWidth, viewportHeight, ViewportScrollbarAxis.Vertical), _verticalScrollbarTrack, _verticalScrollbarThumb, viewportWidth - margin - thickness, margin, verticalLength, minimumThumbLength);
    }

    private static void UpdateScrollbar(ViewportScrollbarAxis axis, ViewportScrollbarMetrics metrics, Border track, Border thumb, double left, double top, double trackLength, double minimumThumbLength)
    {
        if (!metrics.IsScrollable || trackLength <= minimumThumbLength)
        {
            track.Visibility = Visibility.Collapsed;
            thumb.Visibility = Visibility.Collapsed;
            return;
        }

        track.Visibility = Visibility.Visible;
        thumb.Visibility = Visibility.Visible;
        var thumbLength = Math.Clamp(trackLength * metrics.ViewportFraction, minimumThumbLength, trackLength);
        var thumbPosition = (trackLength - thumbLength) * metrics.PositionFraction;
        Canvas.SetLeft(track, left);
        Canvas.SetTop(track, top);
        Canvas.SetLeft(thumb, axis == ViewportScrollbarAxis.Horizontal ? left + thumbPosition : left);
        Canvas.SetTop(thumb, axis == ViewportScrollbarAxis.Vertical ? top + thumbPosition : top);
        if (axis == ViewportScrollbarAxis.Horizontal)
        {
            track.Width = trackLength;
            thumb.Width = thumbLength;
        }
        else
        {
            track.Height = trackLength;
            thumb.Height = thumbLength;
        }
    }

    private async void OnShowBackgroundImagesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showBackgroundImages = ShowBackgroundImagesCheckBox.IsChecked ?? true;
        await RequestRenderAsync();
    }

    private void OnViewportMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverPointerPosition = null;
        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private async void OnShowImageTilesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showImageTiles = ShowImageTilesCheckBox.IsChecked ?? true;
        await RequestRenderAsync();
    }

    private async void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var origin = e.GetPosition(ViewportHost);
        _hoverPointerPosition = origin;
        UpdatePixelometer(origin);
        var requestedScaleDelta = e.Delta > 0 ? 1.15 : 1 / 1.15;

        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);

        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        var zoomDeltas = ViewportZoomPolicy.ComputeWheelDeltas(
            _camera.ScaleX,
            _camera.ScaleY,
            minimumScaleX,
            minimumScaleY,
            requestedScaleDelta);
        if (!zoomDeltas.HasChange)
        {
            return;
        }

        if (_camera.Zoom(zoomDeltas.ScaleX, zoomDeltas.ScaleY, new ScreenPoint(origin.X, origin.Y)))
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
    }

    private void UpdateZoomDisplay(CameraSnapshot camera, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
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
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        var baseUniformScale = Math.Max(minimumScaleX, minimumScaleY);
        var targetScale = baseUniformScale * (percent / 100.0);
        var delta = targetScale / _camera.ScaleX;

        _camera.Zoom(delta, delta, new ScreenPoint(width / 2, height / 2));
    }

    private void ApplyFitToWidthZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleX, minimumScaleY, width, height);
    }

    private void ApplyFitToHeightZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleY, minimumScaleY, width, height);
    }

    private void ApplyScaleWithUniformFirst(double preferredUniformScale, double fallbackScaleY, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
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
        _anchorPanTimer.Stop();
        UnsubscribeTileGenerationEvents(_tiles);
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        FramePresenter.Child = null;
        _frontBitmapFactory?.Dispose();
        _backBitmapFactory?.Dispose();
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
        if (TryReadPixelValue(worldX, worldY, camera, mipLevel, out var backgroundValue, out var defectValue, out var tileId, out var tileInfo))
        {
            var finalValue = ResolveDisplayPixelValue(backgroundValue, worldX, worldY);
            PixelometerTileText.Text = tileInfo.Format();
            PixelometerValueText.Text = $"PIXEL {finalValue}  ({tileId}) bg {backgroundValue} + defect {defectValue}";
            return;
        }

        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private bool TryReadPixelValue(
        double worldX,
        double worldY,
        CameraSnapshot camera,
        int mipLevel,
        out byte background,
        out byte defect,
        out string tileId,
        out BackgroundTileReadoutInfo tileInfo)
    {
        if (_tiles.Count > 0
            && TileGridIndexLookup.TryGetTileIndex(
                worldX,
                worldY,
                _sceneBounds,
                _tiles[0].Bounds.Width,
                _tiles[0].Bounds.Height,
                _tileColumns,
                _tiles.Count,
                out var tileIndex))
        {
            var tile = _tiles[tileIndex];

            // Sample the pixel from the visible/resident mip level rather than
            // forcing native-resolution generation. Convert world -> tile ->
            // mip coordinates using the resident mip dimensions so indexing is
            // safe when a lower-resolution mip is used as a fallback.
            byte[] sourcePixels;
            // Pass the cache budget reservation so that hover-triggered
            // tile generation participates in budget accounting instead of
            // bypassing it (which would create untracked, unevictable tiles).
            var hasSourcePixels = tile.TryGetPixelsNonBlocking(
                mipLevel, out sourcePixels, out var residentMipLevel,
                tryReserveCacheEntry: (cacheKey, byteCost) => _tileCacheBudget.TryReserve(tile, cacheKey, byteCost));

            var sourceDimensions = BackgroundTileMipPolicy.GetDimensions(tile.PixelWidth, tile.PixelHeight, residentMipLevel);
            var sourceX = Math.Clamp((int)((worldX - tile.Bounds.X) * sourceDimensions.Width / tile.Bounds.Width), 0, Math.Max(0, sourceDimensions.Width - 1));
            var sourceY = Math.Clamp((int)((worldY - tile.Bounds.Y) * sourceDimensions.Height / tile.Bounds.Height), 0, Math.Max(0, sourceDimensions.Height - 1));

            if (hasSourcePixels)
            {
                background = sourcePixels[(sourceY * sourceDimensions.Width) + sourceX];
            }
            else
            {
                background = tile.PlaceholderValue;
            }

            defect = 0;
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
            tileInfo = new BackgroundTileReadoutInfo(tile.Id, mipLevel, dimensions.Width, dimensions.Height);
            tileId = tile.Id;
            return true;
        }

        background = default;
        defect = default;
        tileId = string.Empty;
        tileInfo = new BackgroundTileReadoutInfo(string.Empty, mipLevel, 0, 0);
        return false;
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
