using System.Text.Json;

namespace InfiniteCanvas.Core;

public sealed record CanvasUserSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public int TileColumns { get; init; } = 2;

    public int TileRows { get; init; } = 32;

    public int ObjectsPerTile { get; init; } = 16;

    public int AnnotationDisplayMode { get; init; }

    public double OutlineThickness { get; init; } = 2;

    public double LabelSize { get; init; } = 8.5;

    public int LabelDisplay { get; init; }

    public bool ShowLabels { get; init; } = true;

    public bool ShowBoxes { get; init; } = true;

    public bool ShowSparseImageTiles { get; init; } = true;

    public bool ShowBackgroundImages { get; init; } = true;

    public byte BackgroundNoise { get; init; } = 8;

    public int BackgroundCircleCount { get; init; } = 3;

    public double MinimumSparseTilePixelSize { get; init; } = 96;

    public bool IsValid =>
        Version == CurrentVersion
        && TileColumns > 0
        && TileRows > 0
        && (long)TileColumns * TileRows <= 2000
        && ObjectsPerTile >= 0
        && AnnotationDisplayMode is >= 0 and <= 2
        && OutlineThickness is >= 1 and <= 6
        && LabelSize is >= 8 and <= 20
        && LabelDisplay is >= 0 and <= 1
        && BackgroundCircleCount is >= 0 and <= 8
        && MinimumSparseTilePixelSize is >= 0 and <= 4096;
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
            return settings is { IsValid: true } ? settings : new CanvasUserSettings();
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