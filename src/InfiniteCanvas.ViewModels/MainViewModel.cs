using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    public partial TileBackgroundSettingsViewModel TileBackgroundSettings { get; set; } = new();

    public void ApplySettings(CanvasUserSettings settings)
    {
        TileBackgroundSettings.ShowTileLabels = settings.ShowBackgroundTileLabels;
        TileBackgroundSettings.TargetValue = settings.BackgroundTargetValue;
        TileBackgroundSettings.Noise = settings.BackgroundNoise;
        TileBackgroundSettings.CircleCount = settings.BackgroundCircleCount;
        TileBackgroundSettings.Scale = settings.BackgroundNoiseScale;
        TileBackgroundSettings.Octaves = settings.BackgroundNoiseOctaves;
        TileBackgroundSettings.Lacunarity = settings.BackgroundNoiseLacunarity;
        TileBackgroundSettings.Gain = settings.BackgroundNoiseGain;
        TileBackgroundSettings.Amplitude = settings.BackgroundNoiseAmplitude;
    }

    public TileBackgroundSettingsSnapshot CreateBackgroundSettingsSnapshot()
    {
        return new TileBackgroundSettingsSnapshot(
            ShowTileLabels: TileBackgroundSettings.ShowTileLabels,
            TargetValue: (byte)Math.Clamp(Math.Round(TileBackgroundSettings.TargetValue), 0, 255),
            Noise: (byte)Math.Clamp(Math.Round(TileBackgroundSettings.Noise), 0, 24),
            CircleCount: Math.Clamp((int)Math.Round(TileBackgroundSettings.CircleCount), 0, 8),
            NoiseScale: Math.Clamp(TileBackgroundSettings.Scale, 0.01, 8),
            NoiseOctaves: Math.Clamp((int)Math.Round(TileBackgroundSettings.Octaves), 1, 12),
            NoiseLacunarity: Math.Clamp(TileBackgroundSettings.Lacunarity, 0.1, 8),
            NoiseGain: Math.Clamp(TileBackgroundSettings.Gain, 0, 1),
            NoiseAmplitude: Math.Clamp(TileBackgroundSettings.Amplitude, 0, 4));
    }

    public void ApplyBackgroundSettingsSnapshot(TileBackgroundSettingsSnapshot snapshot)
    {
        TileBackgroundSettings.ShowTileLabels = snapshot.ShowTileLabels;
        TileBackgroundSettings.TargetValue = snapshot.TargetValue;
        TileBackgroundSettings.Noise = snapshot.Noise;
        TileBackgroundSettings.CircleCount = snapshot.CircleCount;
        TileBackgroundSettings.Scale = snapshot.NoiseScale;
        TileBackgroundSettings.Octaves = snapshot.NoiseOctaves;
        TileBackgroundSettings.Lacunarity = snapshot.NoiseLacunarity;
        TileBackgroundSettings.Gain = snapshot.NoiseGain;
        TileBackgroundSettings.Amplitude = snapshot.NoiseAmplitude;
    }
}

public sealed record TileBackgroundSettingsSnapshot(
    bool ShowTileLabels,
    byte TargetValue,
    byte Noise,
    int CircleCount,
    double NoiseScale,
    int NoiseOctaves,
    double NoiseLacunarity,
    double NoiseGain,
    double NoiseAmplitude);

public partial class TileBackgroundSettingsViewModel : ObservableObject
{
    // Centralize defaults in CanvasUserSettings so the initial control state
    // can never drift from the canonical persisted settings values.
    private static readonly CanvasUserSettings Defaults = new();

    [ObservableProperty]
    public partial bool ShowTileLabels { get; set; } = Defaults.ShowBackgroundTileLabels;

    [ObservableProperty]
    public partial double TargetValue { get; set; } = Defaults.BackgroundTargetValue;

    [ObservableProperty]
    public partial double Noise { get; set; } = Defaults.BackgroundNoise;

    [ObservableProperty]
    public partial double CircleCount { get; set; } = Defaults.BackgroundCircleCount;

    [ObservableProperty]
    public partial double Scale { get; set; } = Defaults.BackgroundNoiseScale;

    [ObservableProperty]
    public partial double Octaves { get; set; } = Defaults.BackgroundNoiseOctaves;

    [ObservableProperty]
    public partial double Lacunarity { get; set; } = Defaults.BackgroundNoiseLacunarity;

    [ObservableProperty]
    public partial double Gain { get; set; } = Defaults.BackgroundNoiseGain;

    [ObservableProperty]
    public partial double Amplitude { get; set; } = Defaults.BackgroundNoiseAmplitude;
}
