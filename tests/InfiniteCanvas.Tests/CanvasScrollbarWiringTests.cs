namespace InfiniteCanvas.Tests;

[TestFixture]
public class CanvasScrollbarWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Test]
    public void MainWindow_PreservesScrollbarOverlayAndRenderUpdateHook()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Contain("ViewportScrollbarOverlay"));
            Assert.That(xaml, Does.Contain("HorizontalScrollbarTrack"));
            Assert.That(xaml, Does.Contain("VerticalScrollbarTrack"));
            Assert.That(xaml, Does.Contain("OnScrollbarThumbMouseMove"));
            Assert.That(codeBehind, Does.Contain("private void UpdateViewportScrollbars("));
            Assert.That(codeBehind, Does.Contain("UpdateViewportScrollbars(camera, width, height);"));
            Assert.That(codeBehind, Does.Contain("ViewportScrollbarPolicy.ComputeMetrics"));
        });
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
