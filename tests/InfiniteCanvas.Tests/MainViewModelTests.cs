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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.TargetValue, Is.EqualTo((byte)160));
            Assert.That(snapshot.Noise, Is.EqualTo((byte)12));
            Assert.That(snapshot.CircleCount, Is.EqualTo(5));
        }

        mainViewModel.TileBackgroundNoiseSettings.TargetValue = 24;
        mainViewModel.TileBackgroundNoiseSettings.Noise = 2;
        mainViewModel.TileBackgroundNoiseSettings.CircleCount = 1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.TargetValue, Is.EqualTo((byte)160));
            Assert.That(snapshot.Noise, Is.EqualTo((byte)12));
            Assert.That(snapshot.CircleCount, Is.EqualTo(5));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.TargetValue, Is.EqualTo(200));
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.Noise, Is.EqualTo(18));
            Assert.That(mainViewModel.TileBackgroundNoiseSettings.CircleCount, Is.EqualTo(6));
        }
    }

    [Test]
    public void Regeneration_RestoresEveryEditedNoiseFieldIntoFreshViewModel()
    {
        var edited = new MainViewModel();
        edited.TileBackgroundNoiseSettings.TargetValue = 160;
        edited.TileBackgroundNoiseSettings.Noise = 12;
        edited.TileBackgroundNoiseSettings.CircleCount = 5;
        edited.TileBackgroundNoiseSettings.Scale = 2.5;
        edited.TileBackgroundNoiseSettings.Octaves = 7;
        edited.TileBackgroundNoiseSettings.Lacunarity = 3.1;
        edited.TileBackgroundNoiseSettings.Gain = 0.4;
        edited.TileBackgroundNoiseSettings.Amplitude = 2.2;

        // This snapshot is the generation input captured before regeneration.
        var generationInput = edited.CreateBackgroundNoiseSnapshot();

        // Simulate RegenerateSceneAsync publishing a fresh MainViewModel and
        // restoring the captured snapshot so the controls keep the edited values.
        var regenerated = new MainViewModel();
        regenerated.ApplyBackgroundNoiseSnapshot(generationInput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(regenerated.TileBackgroundNoiseSettings.TargetValue, Is.EqualTo(160.0));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Noise, Is.EqualTo(12.0));
            Assert.That(regenerated.TileBackgroundNoiseSettings.CircleCount, Is.EqualTo(5.0));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Scale, Is.EqualTo(2.5));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Octaves, Is.EqualTo(7.0));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Lacunarity, Is.EqualTo(3.1));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Gain, Is.EqualTo(0.4));
            Assert.That(regenerated.TileBackgroundNoiseSettings.Amplitude, Is.EqualTo(2.2));
        }

        // The generator must receive the same snapshot that remains in the
        // bound view model after regeneration.
        Assert.That(regenerated.CreateBackgroundNoiseSnapshot(), Is.EqualTo(generationInput));
    }

    [Test]
    public void TileBackgroundNoiseSettings_DefaultsMatchCanvasUserSettings()
    {
        var settings = new CanvasUserSettings();
        var viewModel = new TileBackgroundNoiseSettingsViewModel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.TargetValue, Is.EqualTo(settings.BackgroundTargetValue));
            Assert.That(viewModel.Noise, Is.EqualTo(settings.BackgroundNoise));
            Assert.That(viewModel.CircleCount, Is.EqualTo(settings.BackgroundCircleCount));
            Assert.That(viewModel.Scale, Is.EqualTo(settings.BackgroundNoiseScale));
            Assert.That(viewModel.Octaves, Is.EqualTo(settings.BackgroundNoiseOctaves));
            Assert.That(viewModel.Lacunarity, Is.EqualTo(settings.BackgroundNoiseLacunarity));
            Assert.That(viewModel.Gain, Is.EqualTo(settings.BackgroundNoiseGain));
            Assert.That(viewModel.Amplitude, Is.EqualTo(settings.BackgroundNoiseAmplitude));
        }
    }
}
