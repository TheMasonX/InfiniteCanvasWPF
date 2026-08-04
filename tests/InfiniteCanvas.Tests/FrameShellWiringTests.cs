namespace InfiniteCanvas.Tests;

[TestFixture]
public class FrameShellWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MainWindowCodeBehind =
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

    [Test]
    public void MainWindow_BuildsFrameShellOnceAndReusesIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MainWindowCodeBehind, Does.Contain("private void EnsureFrameShell()"),
                "The persistent frame shell method must exist.");
            Assert.That(MainWindowCodeBehind, Does.Contain("FramePresenter.Child = shell;"),
                "The frame shell must be attached to the Viewbox once.");
            Assert.That(MainWindowCodeBehind, Does.Contain("EnsureFrameShell();"),
                "PublishFrame must reuse the shell instead of rebuilding the tree.");
        }
    }

    [Test]
    public void PublishFrame_DoesNotReplaceViewboxChildPerFrame()
    {
        // Reassigning the Viewbox child on every publish tore down and rebuilt
        // the visual tree per frame, causing occasional black flashes while
        // scrolling. The child is now assigned exactly twice: the shell attach
        // in EnsureFrameShell and the detach in OnClosed.
        var assignments = MainWindowCodeBehind.Split("FramePresenter.Child =").Length - 1;

        Assert.That(assignments, Is.EqualTo(2),
            "FramePresenter.Child must be assigned only for shell attach and close detach.");
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
