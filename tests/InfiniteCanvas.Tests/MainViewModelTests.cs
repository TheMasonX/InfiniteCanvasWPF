using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class MainViewModelTests
{
    [Test]
    public void TileBackgroundSettingsSnapshot_CopiesValuesAndRemainsStable()
    {
        var mainViewModel = new MainViewModel();
        mainViewModel.TileBackgroundSettings.TargetValue = 160;
        mainViewModel.TileBackgroundSettings.Noise = 12;
        mainViewModel.TileBackgroundSettings.CircleCount = 5;

        var snapshot = mainViewModel.CreateBackgroundSettingsSnapshot();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.TargetValue, Is.EqualTo((byte)160));
            Assert.That(snapshot.Noise, Is.EqualTo((byte)12));
            Assert.That(snapshot.CircleCount, Is.EqualTo(5));
        }

        mainViewModel.TileBackgroundSettings.TargetValue = 24;
        mainViewModel.TileBackgroundSettings.Noise = 2;
        mainViewModel.TileBackgroundSettings.CircleCount = 1;

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
            Assert.That(mainViewModel.TileBackgroundSettings.TargetValue, Is.EqualTo(200));
            Assert.That(mainViewModel.TileBackgroundSettings.Noise, Is.EqualTo(18));
            Assert.That(mainViewModel.TileBackgroundSettings.CircleCount, Is.EqualTo(6));
        }
    }

    [Test]
    public void Regeneration_RestoresEveryEditedBackgroundFieldIntoFreshViewModel()
    {
        var edited = new MainViewModel();
        edited.TileBackgroundSettings.TargetValue = 160;
        edited.TileBackgroundSettings.Noise = 12;
        edited.TileBackgroundSettings.CircleCount = 5;
        edited.TileBackgroundSettings.Scale = 2.5;
        edited.TileBackgroundSettings.Octaves = 7;
        edited.TileBackgroundSettings.Lacunarity = 3.1;
        edited.TileBackgroundSettings.Gain = 0.4;
        edited.TileBackgroundSettings.Amplitude = 2.2;

        // This snapshot is the generation input captured before regeneration.
        var generationInput = edited.CreateBackgroundSettingsSnapshot();

        // Simulate RegenerateSceneAsync publishing a fresh MainViewModel and
        // restoring the captured snapshot so the controls keep the edited values.
        var regenerated = new MainViewModel();
        regenerated.ApplyBackgroundSettingsSnapshot(generationInput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(regenerated.TileBackgroundSettings.TargetValue, Is.EqualTo(160.0));
            Assert.That(regenerated.TileBackgroundSettings.Noise, Is.EqualTo(12.0));
            Assert.That(regenerated.TileBackgroundSettings.CircleCount, Is.EqualTo(5.0));
            Assert.That(regenerated.TileBackgroundSettings.Scale, Is.EqualTo(2.5));
            Assert.That(regenerated.TileBackgroundSettings.Octaves, Is.EqualTo(7.0));
            Assert.That(regenerated.TileBackgroundSettings.Lacunarity, Is.EqualTo(3.1));
            Assert.That(regenerated.TileBackgroundSettings.Gain, Is.EqualTo(0.4));
            Assert.That(regenerated.TileBackgroundSettings.Amplitude, Is.EqualTo(2.2));
        }

        // The generator must receive the same snapshot that remains in the
        // bound view model after regeneration.
        Assert.That(regenerated.CreateBackgroundSettingsSnapshot(), Is.EqualTo(generationInput));
    }

    [Test]
    public void TileBackgroundSettings_DefaultsMatchCanvasUserSettings()
    {
        var settings = new CanvasUserSettings();
        var viewModel = new TileBackgroundSettingsViewModel();

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
