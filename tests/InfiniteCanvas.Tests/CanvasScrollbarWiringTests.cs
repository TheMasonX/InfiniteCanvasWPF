namespace InfiniteCanvas.Tests;

[TestFixture]
public class CanvasScrollbarWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Test]
    public void CanvasControl_PreservesScrollbarOverlayAndRenderUpdateHook()
    {
        var mainWindowXaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml"));
        var mainWindowCodeBehind = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));
        var canvasControlXaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.Controls", "CanvasControl.xaml"));
        var canvasControlCodeBehind = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.Controls", "CanvasControl.xaml.cs"));

        using (Assert.EnterMultipleScope())
        {
            // The custom scrollbar overlay moved to the extracted canvas
            // control. The overlay, tracks, thumbs, and drag handlers must stay
            // present together (functional-requirements-and-invariants.md).
            Assert.That(canvasControlXaml, Does.Contain("ViewportScrollbarOverlay"));
            Assert.That(canvasControlXaml, Does.Contain("HorizontalScrollbarTrack"));
            Assert.That(canvasControlXaml, Does.Contain("VerticalScrollbarTrack"));
            Assert.That(canvasControlXaml, Does.Contain("OnScrollbarThumbMouseMove"));
            // The window keeps native scrollbars hidden and reserves right-side
            // padding so the overlay stays clear of the panel (ICW-094).
            Assert.That(mainWindowXaml, Does.Contain("HorizontalScrollBarVisibility=\"Disabled\""));
            Assert.That(mainWindowXaml, Does.Contain("Padding=\"0,0,14,0\""));
            Assert.That(mainWindowXaml, Does.Contain("controls:CanvasControl"));
            // Scrollbar metrics and positioning live on the canvas control.
            Assert.That(canvasControlCodeBehind, Does.Contain("private void UpdateViewportScrollbars("));
            Assert.That(canvasControlCodeBehind, Does.Contain("ViewportScrollbarPolicy.ComputeMetrics"));
            // The render-update hook is exposed to the window and invoked after
            // the camera is clamped on the render/pan path.
            Assert.That(canvasControlCodeBehind, Does.Contain("public void RefreshScrollbars()"));
            Assert.That(mainWindowCodeBehind, Does.Contain("CanvasSurface.RefreshScrollbars()"));
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
