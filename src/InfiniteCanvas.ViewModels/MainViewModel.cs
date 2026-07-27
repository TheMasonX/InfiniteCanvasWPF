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
    }

    public TileBackgroundNoiseSnapshot CreateBackgroundNoiseSnapshot()
    {
        return new TileBackgroundNoiseSnapshot(
            TargetValue: (byte)Math.Clamp(Math.Round(TileBackgroundNoiseSettings.TargetValue), 0, 255),
            Noise: (byte)Math.Clamp(Math.Round(TileBackgroundNoiseSettings.Noise), 0, 24),
            CircleCount: Math.Clamp((int)Math.Round(TileBackgroundNoiseSettings.CircleCount), 0, 8));
    }
}

public sealed record TileBackgroundNoiseSnapshot(byte TargetValue, byte Noise, int CircleCount);

public partial class TileBackgroundNoiseSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private double targetValue = 128;

    [ObservableProperty]
    private double noise = 8;

    [ObservableProperty]
    private double circleCount = 3;
}
