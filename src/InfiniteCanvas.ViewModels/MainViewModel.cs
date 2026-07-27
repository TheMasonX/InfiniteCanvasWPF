using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int visibleItemCount;

    [ObservableProperty]
    private int totalItemCount;

    [ObservableProperty]
    private TileBackgroundNoiseSettingsViewModel tileBackgroundNoiseSettings = new();

    public void ApplyViewportState(int visibleItemCount, int totalItemCount)
    {
        VisibleItemCount = visibleItemCount;
        TotalItemCount = totalItemCount;
    }

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
    [ObservableProperty]
    private double targetValue = 128;

    [ObservableProperty]
    private double noise = 8;

    [ObservableProperty]
    private double circleCount = 3;

    [ObservableProperty]
    private double scale = 1;

    [ObservableProperty]
    private double octaves = 5;

    [ObservableProperty]
    private double lacunarity = 2.5;

    [ObservableProperty]
    private double gain = 0.6;

    [ObservableProperty]
    private double amplitude = 1;
}
