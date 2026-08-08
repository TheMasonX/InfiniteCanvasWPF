namespace InfiniteCanvas.Rendering;

/// <summary>
/// Adapts generated sample tiles to the source-neutral background material contract.
/// </summary>
public sealed class SampleImageTileSource : IBackgroundTileSource
{
    private readonly Lock _gate = new();
    private IReadOnlyDictionary<string, SampleImageTile> _tiles =
        new Dictionary<string, SampleImageTile>(StringComparer.Ordinal);

    public void SetTiles(IEnumerable<SampleImageTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var snapshot = tiles.ToDictionary(tile => tile.Id, StringComparer.Ordinal);
        lock (_gate)
        {
            _tiles = snapshot;
        }
    }

    public ValueTask<BackgroundTilePayload> ResolveAsync(
        BackgroundTileRequest request,
        CancellationToken cancellationToken = default)
    {
        SampleImageTile tile;
        lock (_gate)
        {
            if (!_tiles.TryGetValue(request.Descriptor.TileId, out tile!))
            {
                throw new KeyNotFoundException($"Sample tile '{request.Descriptor.TileId}' was not found.");
            }
        }

        return tile.ResolveBackgroundTileAsync(request, cancellationToken);
    }
}