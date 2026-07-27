namespace InfiniteCanvas.Rendering;

public sealed record GeneratorOptions(
    int ImageCount = SampleImageGenerator.DefaultPixelWidth, // placeholder, overwritten by default usage
    int PixelWidth = SampleImageGenerator.DefaultPixelWidth,
    int PixelHeight = SampleImageGenerator.DefaultPixelHeight,
    byte TargetValue = 128,
    byte Noise = 8,
    double NoiseScale = 1.0,
    int NoiseOctaves = 3,
    double NoiseLacunarity = 2.5,
    double NoiseGain = 0.6,
    double NoiseAmplitude = 1.0,
    int ObjectsPerTile = 16,
    int Columns = 2,
    int Seed = 1729,
    int? Rows = null,
    int DefectPoolSize = 64,
    int CircleCount = 3
);
