using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private TileBackgroundNoiseSettingsViewModel tileBackgroundNoiseSettings = new();

    public void ApplySettings(CanvasUserSettings settings)
    {
        TileBackgroundNoiseSettings.TargetValue = settings.BackgroundTargetValue;
        TileBackgroundNoiseSettings.Noise = settings.BackgroundNoise;
        TileBackgroundNoiseSettings.CircleCount = settings.BackgroundCircleCount;
        TileBackgroundNoiseSettings.Scale = settings.BackgroundNoiseScale;
        TileBackgroundNoiseSettings.Octaves = settings.BackgroundNoiseOctaves;
        TileBackgroundNoiseSettings.Lacunarity = settings.BackgroundNoiseLacunarity;
        TileBackgroundNoiseSettings.Gain = settings.BackgroundNoiseGain;
        TileBackgroundNoiseSettings.Amplitude = settings.BackgroundNoiseAmplitude;
    }

    public TileBackgroundNoiseSnapshot CreateBackgroundNoiseSnapshot()
    {
        return new TileBackgroundNoiseSnapshot(
            TargetValue: (byte)Math.Clamp(Math.Round(TileBackgroundNoiseSettings.TargetValue), 0, 255),
            Noise: (byte)Math.Clamp(Math.Round(TileBackgroundNoiseSettings.Noise), 0, 24),
            CircleCount: Math.Clamp((int)Math.Round(TileBackgroundNoiseSettings.CircleCount), 0, 8),
            NoiseScale: Math.Clamp(TileBackgroundNoiseSettings.Scale, 0.01, 8),
            NoiseOctaves: Math.Clamp((int)Math.Round(TileBackgroundNoiseSettings.Octaves), 1, 12),
            NoiseLacunarity: Math.Clamp(TileBackgroundNoiseSettings.Lacunarity, 0.1, 8),
            NoiseGain: Math.Clamp(TileBackgroundNoiseSettings.Gain, 0, 1),
            NoiseAmplitude: Math.Clamp(TileBackgroundNoiseSettings.Amplitude, 0, 4));
    }

    public void ApplyBackgroundNoiseSnapshot(TileBackgroundNoiseSnapshot snapshot)
    {
        TileBackgroundNoiseSettings.TargetValue = snapshot.TargetValue;
        TileBackgroundNoiseSettings.Noise = snapshot.Noise;
        TileBackgroundNoiseSettings.CircleCount = snapshot.CircleCount;
        TileBackgroundNoiseSettings.Scale = snapshot.NoiseScale;
        TileBackgroundNoiseSettings.Octaves = snapshot.NoiseOctaves;
        TileBackgroundNoiseSettings.Lacunarity = snapshot.NoiseLacunarity;
        TileBackgroundNoiseSettings.Gain = snapshot.NoiseGain;
        TileBackgroundNoiseSettings.Amplitude = snapshot.NoiseAmplitude;
    }
}

public sealed record TileBackgroundNoiseSnapshot(
    byte TargetValue,
    byte Noise,
    int CircleCount,
    double NoiseScale,
    int NoiseOctaves,
    double NoiseLacunarity,
    double NoiseGain,
    double NoiseAmplitude);

public partial class TileBackgroundNoiseSettingsViewModel : ObservableObject
{
    // Centralize defaults in CanvasUserSettings so the initial control state
    // can never drift from the canonical persisted settings values.
    private static readonly CanvasUserSettings Defaults = new();

    [ObservableProperty]
    private double targetValue = Defaults.BackgroundTargetValue;

    [ObservableProperty]
    private double noise = Defaults.BackgroundNoise;

    [ObservableProperty]
    private double circleCount = Defaults.BackgroundCircleCount;

    [ObservableProperty]
    private double scale = Defaults.BackgroundNoiseScale;

    [ObservableProperty]
    private double octaves = Defaults.BackgroundNoiseOctaves;

    [ObservableProperty]
    private double lacunarity = Defaults.BackgroundNoiseLacunarity;

    [ObservableProperty]
    private double gain = Defaults.BackgroundNoiseGain;

    [ObservableProperty]
    private double amplitude = Defaults.BackgroundNoiseAmplitude;
}
