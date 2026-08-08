namespace InfiniteCanvas.Tests;

[TestFixture]
public class TileGenerationSettingsWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MainWindowMarkup = File.ReadAllText(
        Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml"));
    private static readonly string MainWindowCodeBehind = File.ReadAllText(
        Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

    [Test]
    public void TileGenerationSettings_ExposeAndConsumeCountAndPixelDimensions()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MainWindowMarkup, Does.Contain("Maximum=\"24288\""));
            Assert.That(MainWindowMarkup, Does.Contain("TilePixelWidthSliderTextBox"));
            Assert.That(MainWindowMarkup, Does.Contain("TilePixelHeightSliderTextBox"));
            Assert.That(MainWindowMarkup, Does.Contain("Value=\"8192\""));
            Assert.That(MainWindowMarkup, Does.Contain("Value=\"4096\""));
            Assert.That(MainWindowCodeBehind, Does.Contain("_tilePixelWidth = settings.TilePixelWidth"));
            Assert.That(MainWindowCodeBehind, Does.Contain("_tilePixelHeight = settings.TilePixelHeight"));
            Assert.That(MainWindowCodeBehind, Does.Contain("pixelWidth: _tilePixelWidth"));
            Assert.That(MainWindowCodeBehind, Does.Contain("pixelHeight: _tilePixelHeight"));
            Assert.That(MainWindowCodeBehind, Does.Contain("CanvasUserSettings.ValidateTileCount"));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InfiniteCanvasWPF.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}