namespace InfiniteCanvas.Core;

public readonly record struct CanvasLayerRevisionVector(
    long Background,
    long Defect,
    long TileGrid,
    long Annotations,
    long Pixelometer);

public readonly record struct CanvasFrameIdentity
{
    public CanvasFrameIdentity(
        string sourceSessionId,
        long sceneRevision,
        CanvasLayerRevisionVector layerRevisions,
        long displayRevision,
        long selectionRevision,
        long renderSequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSessionId);
        SourceSessionId = sourceSessionId;
        SceneRevision = sceneRevision;
        LayerRevisions = layerRevisions;
        DisplayRevision = displayRevision;
        SelectionRevision = selectionRevision;
        RenderSequence = renderSequence;
    }

    public string SourceSessionId { get; }

    public long SceneRevision { get; }

    public CanvasLayerRevisionVector LayerRevisions { get; }

    public long DisplayRevision { get; }

    public long SelectionRevision { get; }

    public long RenderSequence { get; }

    public static CanvasFrameIdentity Default(long renderSequence = 0) =>
        new("default", 0, default, 0, 0, renderSequence);

    public bool CanReplace(CanvasFrameIdentity previous)
    {
        if (!string.Equals(SourceSessionId, previous.SourceSessionId, StringComparison.Ordinal))
        {
            return true;
        }

        return SceneRevision >= previous.SceneRevision
            && LayerRevisions.Background >= previous.LayerRevisions.Background
            && LayerRevisions.Defect >= previous.LayerRevisions.Defect
            && LayerRevisions.TileGrid >= previous.LayerRevisions.TileGrid
            && LayerRevisions.Annotations >= previous.LayerRevisions.Annotations
            && LayerRevisions.Pixelometer >= previous.LayerRevisions.Pixelometer
            && DisplayRevision >= previous.DisplayRevision
            && SelectionRevision >= previous.SelectionRevision
            && RenderSequence >= previous.RenderSequence;
    }
}

public enum CanvasLayerKind
{
    Raster,
    BackgroundMaterial,
    DefectImagery,
    TileGrid,
    Annotations,
    Labels,
    Selection,
    Pixelometer
}

public readonly record struct CanvasLayerDescriptor(
    CanvasLayerKind Kind,
    bool IsVisible,
    long Revision);

public sealed class CanvasLayerPlan
{
    private readonly CanvasLayerDescriptor[] _layers;

    public CanvasLayerPlan(IEnumerable<CanvasLayerDescriptor> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        _layers = layers.ToArray();
        if (_layers.Length == 0)
        {
            throw new ArgumentException("A layer plan must contain at least one layer.", nameof(layers));
        }

        for (var index = 1; index < _layers.Length; index++)
        {
            if (_layers[index - 1].Kind >= _layers[index].Kind)
            {
                throw new ArgumentException("Layer descriptors must use deterministic enum order without duplicates.", nameof(layers));
            }
        }
    }

    public IReadOnlyList<CanvasLayerDescriptor> Layers => Array.AsReadOnly(_layers);

    public bool IsVisible(CanvasLayerKind kind)
    {
        return _layers.First(layer => layer.Kind == kind).IsVisible;
    }
}

public sealed class CanvasSceneChangedEventArgs(CanvasFrameIdentity identity) : EventArgs
{
    public CanvasFrameIdentity Identity { get; } = identity;
}