namespace InfiniteCanvas.Rendering;

public sealed record MipOptions(
    int MipLevel = 0,
    int Seed = 1729,
    int CircleCount = 3,
    SampleImageGenerator.NoiseSettings? NoiseSettings = null,
    float WorldOriginX = 0f,
    float WorldOriginY = 0f,
    string? TileLabel = null
);
