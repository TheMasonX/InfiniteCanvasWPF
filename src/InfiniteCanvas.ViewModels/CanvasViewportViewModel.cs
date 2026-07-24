using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.ViewModels;

public partial class CanvasViewportViewModel<T> : ObservableObject where T : ISpatialEntity
{
    private readonly ISpatialIndexService<T> _spatialIndexService;

    public CanvasViewportViewModel(ISpatialIndexService<T> spatialIndexService)
    {
        _spatialIndexService = spatialIndexService;
    }

    [ObservableProperty]
    private SpatialBounds viewport;

    [ObservableProperty]
    private int visibleItemCount;

    [ObservableProperty]
    private int totalItemCount;

    [ObservableProperty]
    private DateTimeOffset? lastSnapshotPublishedAtUtc;

    public void ApplyFrame(SpatialBounds viewport, int visibleItemCount)
    {
        Viewport = viewport;
        VisibleItemCount = visibleItemCount;
        TotalItemCount = _spatialIndexService.Count;

        if (_spatialIndexService is LiveSpatialIndexService<T> liveSpatialIndexService)
        {
            LastSnapshotPublishedAtUtc = liveSpatialIndexService.LastPublishedAtUtc;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var viewport = Viewport;
        var visibleItemCount = await Task.Run(
            () => _spatialIndexService.Query(viewport).Count,
            cancellationToken);

        VisibleItemCount = visibleItemCount;
        TotalItemCount = _spatialIndexService.Count;

        if (_spatialIndexService is LiveSpatialIndexService<T> liveSpatialIndexService)
        {
            LastSnapshotPublishedAtUtc = liveSpatialIndexService.LastPublishedAtUtc;
        }
    }
}
