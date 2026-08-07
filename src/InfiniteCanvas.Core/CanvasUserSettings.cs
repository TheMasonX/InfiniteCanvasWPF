using System.Text.Json;

namespace InfiniteCanvas.Core;

public sealed record CanvasUserSettings
{
    public const int CurrentVersion = 1;

    /// <summary>
    /// Upper bound for objects per tile, shared with
    /// <see cref="InfiniteCanvas.Rendering.SampleImageGenerator.MaxObjectsPerTile"/>.
    /// Tests assert the two constants stay equal.
    /// </summary>
    public const int MaxObjectsPerTile = 256;

    public const double DefaultMinimumSparseTilePixelSize = 0;

    public const double LegacyDefaultMinimumSparseTilePixelSize = 96;

    public int Version { get; init; } = CurrentVersion;

    public int TileColumns { get; init; } = 2;

    public int TileRows { get; init; } = 32;

    public int ObjectsPerTile { get; init; } = 16;

    public int GenerationSeed { get; init; } = 1729;

    public int AnnotationDisplayMode { get; init; }

    public double OutlineThickness { get; init; } = 2;

    public double LabelSize { get; init; } = 8.5;

    public int LabelDisplay { get; init; }

    public bool ShowLabels { get; init; } = true;

    public bool ShowBoxes { get; init; } = true;

    public bool ShowSparseImageTiles { get; init; } = true;

    public bool ShowImageTiles { get; init; } = true;

    public bool ShowBackgroundImages { get; init; } = true;

    public byte BackgroundTargetValue { get; init; } = 128;

    public byte BackgroundNoise { get; init; } = 8;

    public int BackgroundCircleCount { get; init; } = 3;

    public double BackgroundNoiseScale { get; init; } = 1;

    public int BackgroundNoiseOctaves { get; init; } = 5;

    public double BackgroundNoiseLacunarity { get; init; } = 2.5;

    public double BackgroundNoiseGain { get; init; } = 0.6;

    public double BackgroundNoiseAmplitude { get; init; } = 1;

    public double MinimumSparseTilePixelSize { get; init; } = DefaultMinimumSparseTilePixelSize;

    public static bool ValidateObjectsPerTile(int value) => value is >= 0 and <= MaxObjectsPerTile;

    public static bool ValidateMinimumSparseTilePixelSize(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 4096;

    public bool IsValid =>
        Version == CurrentVersion
        && TileColumns > 0
        && TileRows > 0
        && (long)TileColumns * TileRows <= 2000
        && ValidateObjectsPerTile(ObjectsPerTile)
        && AnnotationDisplayMode is >= 0 and <= 2
        && OutlineThickness is >= 1 and <= 6
        && LabelSize is >= 8 and <= 20
        && LabelDisplay is >= 0 and <= 1
        && BackgroundCircleCount is >= 0 and <= 8
        && BackgroundNoiseScale is >= 0.01 and <= 8
        && BackgroundNoiseOctaves is >= 1 and <= 12
        && BackgroundNoiseLacunarity is >= 0.1 and <= 8
        && BackgroundNoiseGain is >= 0 and <= 1
        && BackgroundNoiseAmplitude is >= 0 and <= 4
        && ValidateMinimumSparseTilePixelSize(MinimumSparseTilePixelSize);
}

public static class CanvasUserSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static CanvasUserSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (!File.Exists(path))
            {
                return new CanvasUserSettings();
            }

            var settings = JsonSerializer.Deserialize<CanvasUserSettings>(File.ReadAllText(path), SerializerOptions);
            if (settings is not { IsValid: true })
            {
                return new CanvasUserSettings();
            }

            // Migrate the previous demo default. A valid settings file can
            // outlive a code default, so changing the property initializer
            // alone does not remove the old 96-pixel background gate.
            return settings.MinimumSparseTilePixelSize == CanvasUserSettings.LegacyDefaultMinimumSparseTilePixelSize
                ? settings with { MinimumSparseTilePixelSize = CanvasUserSettings.DefaultMinimumSparseTilePixelSize }
                : settings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CanvasUserSettings();
        }
    }

    public static void Save(string path, CanvasUserSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid)
        {
            throw new ArgumentException("Settings values are outside the supported range.", nameof(settings));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temporaryPath, path, true);
    }
}