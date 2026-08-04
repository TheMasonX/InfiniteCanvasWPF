namespace InfiniteCanvas.Tests;

/// <summary>
/// Guards the persistent frame-shell and CanvasFrame boundary invariants
/// (ICW-317 no-flash, ICW-315 frame boundary). The control owns the shell
/// and raster display; the host keeps the render pipeline and overlay
/// composition.
/// </summary>
[TestFixture]
public class FrameShellWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string ControlCodeBehind =
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "Controls", "CanvasControl.xaml.cs"));
    private static readonly string MainWindowCodeBehind =
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

    [Test]
    public void CanvasControl_BuildsFrameShellOnceAndReusesIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControlCodeBehind, Does.Contain("private void EnsureFrameShell()"),
                "The persistent frame shell method must live in the control (ICW-315).");
            Assert.That(ControlCodeBehind, Does.Contain("FramePresenter.Child = shell;"),
                "The frame shell must be attached to the Viewbox once.");
            Assert.That(ControlCodeBehind, Does.Contain("EnsureFrameShell();"),
                "PublishFrame must reuse the shell instead of rebuilding the tree.");
        }
    }

    [Test]
    public void PublishFrame_DoesNotReplaceViewboxChildPerFrame()
    {
        // Reassigning the Viewbox child on every publish tore down and rebuilt
        // the visual tree per frame, causing occasional black flashes while
        // scrolling. The child is now assigned exactly twice: the shell attach
        // in EnsureFrameShell and the detach in DetachFrameShell.
        var assignments = ControlCodeBehind.Split("FramePresenter.Child =").Length - 1;

        Assert.That(assignments, Is.EqualTo(2),
            "FramePresenter.Child must be assigned only for shell attach and detach.");
    }

    [Test]
    public void PublishFrame_AcceptsCanvasFrame_NotAUIElementTree()
    {
        // ICW-315: PublishFrame(UIElement) is not a valid library boundary.
        // The control must receive a CanvasFrame value.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControlCodeBehind, Does.Contain("public void PublishFrame(CanvasFrame frame)"),
                "PublishFrame must accept a CanvasFrame, never a UIElement tree.");
            Assert.That(ControlCodeBehind, Does.Not.Contain("PublishFrame(UIElement"),
                "The UIElement overload must be gone.");
            Assert.That(ControlCodeBehind, Does.Contain("FramePublished?.Invoke"),
                "The control must raise FramePublished so the host can compose overlays.");
        }
    }

    [Test]
    public void CanvasControl_NeverTouchesTheRasterMemorySection()
    {
        // Zero-copy handoff (ICW-315, ICW-P0-BUFFER-REUSE-SYNC): the control
        // displays the frozen ImageSource and must never touch the backing
        // buffer types.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControlCodeBehind, Does.Not.Contain("ZeroCopyBitmapFactory"));
            Assert.That(ControlCodeBehind, Does.Not.Contain("InteropBitmap"));
            Assert.That(ControlCodeBehind, Does.Not.Contain("FrameBufferPool"));
        }
    }

    [Test]
    public void MainWindow_PublishesCanvasFrame_AndNoLongerOwnsTheShell()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MainWindowCodeBehind, Does.Contain("new CanvasFrame("),
                "The host must build a CanvasFrame for the boundary.");
            Assert.That(MainWindowCodeBehind, Does.Not.Contain("FramePresenter.Child"),
                "The host must not attach the Viewbox child directly (ICW-315).");
            Assert.That(MainWindowCodeBehind, Does.Not.Contain("private void EnsureFrameShell()"),
                "The shell must live in the control, not the host.");
            Assert.That(MainWindowCodeBehind, Does.Contain("CanvasSurface.PublishFrame(frame)"),
                "The host must publish through the control boundary.");
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
