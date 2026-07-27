using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class MainViewModelTests
{
    [Test]
    public void TileBackgroundNoiseSnapshot_CopiesValuesAndRemainsStable()
    {
        var mainViewModel = new MainViewModel();
        mainViewModel.TileBackgroundNoiseSettings.TargetValue = 160;
        mainViewModel.TileBackgroundNoiseSettings.Noise = 12;
        mainViewModel.TileBackgroundNoiseSettings.CircleCount = 5;

        var snapshot = mainViewModel.CreateBackgroundNoiseSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.TargetValue, Is.EqualTo((byte)160));
            Assert.That(snapshot.Noise, Is.EqualTo((byte)12));
            Assert.That(snapshot.CircleCount, Is.EqualTo(5));
        });

        mainViewModel.TileBackgroundNoiseSettings.TargetValue = 24;
        mainViewModel.TileBackgroundNoiseSettings.Noise = 2;
        mainViewModel.TileBackgroundNoiseSettings.CircleCount = 1;

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.TargetValue, Is.EqualTo((byte)160));
            Assert.That(snapshot.Noise, Is.EqualTo((byte)12));
            Assert.That(snapshot.CircleCount, Is.EqualTo(5));
        });
    }

    [Test]
    public void MainViewModel_AppliesSettingsToChildViewModel()
    {
        var mainViewModel = new MainViewModel();
        var settings = new CanvasUserSettings
        {
            BackgroundTargetValue = 200,
            BackgroundNoise = 18,
            BackgroundCircleCount = 6
        };

        mainViewModel.ApplySettings(settings);

        Assert.Multiple(() =>
        {
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.TargetValue, Is.EqualTo(200));
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.Noise, Is.EqualTo(18));
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.CircleCount, Is.EqualTo(6));
        });
    }
}
