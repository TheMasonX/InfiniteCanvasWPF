namespace InfiniteCanvas.Tests;

[TestFixture]
public class LayerVisibilityWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MainWindowMarkup = File.ReadAllText(
        Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml"));
    private static readonly string MainWindowCodeBehind = File.ReadAllText(
        Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));
    private static readonly string BackgroundSettingsMarkup = File.ReadAllText(
        Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "Controls", "TileBackgroundSettingsView.xaml"));

    [Test]
    public void LayerVisibilitySettings_UseIndependentControlsAndRenderPaths()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MainWindowMarkup, Does.Contain("ShowLabelsCheckBox"));
            Assert.That(MainWindowMarkup, Does.Contain("ShowBoxesCheckBox"));
            Assert.That(MainWindowMarkup, Does.Contain("ShowImageTilesCheckBox"));
            Assert.That(MainWindowMarkup, Does.Contain("ShowSparseImageTilesCheckBox"));
            Assert.That(MainWindowMarkup, Does.Contain("ShowBackgroundImagesCheckBox"));
            Assert.That(BackgroundSettingsMarkup, Does.Contain("Show tile labels"));
            Assert.That(MainWindowCodeBehind, Does.Contain("ShowBoxes = ShowBoxesCheckBox.IsChecked ?? true"));
            Assert.That(MainWindowCodeBehind, Does.Contain("ShowSparseImageTiles = _showSparseImageTiles"));
            Assert.That(MainWindowCodeBehind, Does.Contain("showSparseImageTiles: _showSparseImageTiles"));
            Assert.That(MainWindowCodeBehind, Does.Contain("ShowBoxes ? outlineBrush : null"));
            Assert.That(MainWindowCodeBehind, Does.Contain("ShowSparseImageTilesCheckBox.IsChecked = settings.ShowSparseImageTiles"));
            Assert.That(MainWindowCodeBehind, Does.Contain("ShowBackgroundTileLabels = _mainViewModel.TileBackgroundSettings.ShowTileLabels"));
            Assert.That(MainWindowCodeBehind, Does.Not.Contain("OnTileBackgroundSettingsChanged"));
            Assert.That(MainWindowCodeBehind, Does.Not.Contain("RegenerateForTileLabelSettingAsync"));
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
