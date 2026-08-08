namespace InfiniteCanvas.Tests;

[TestFixture]
public sealed class AnnotationOverlayPoolingWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Test]
    public void MainWindow_UsesRetainedAnnotationVisualsAndUnregistersStaleEntries()
    {
        var codeBehind = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(codeBehind, Does.Contain("Dictionary<string, AnnotationOverlayState>"));
            Assert.That(codeBehind, Does.Contain("MaxDetachedAnnotationOverlayStates = 256"));
            Assert.That(codeBehind, Does.Contain("_detachedAnnotationOverlayStates.TryPop"));
            Assert.That(codeBehind, Does.Contain("AreEquivalentAnnotationItems"));
            Assert.That(codeBehind, Does.Contain("CanvasSurface.UnregisterItemVisual(pair.Value.Element);"));
            Assert.That(codeBehind, Does.Contain("if (!state.IsSelected)"));
            Assert.That(codeBehind, Does.Contain("LogAnnotationDiagnostics();"));
            Assert.That(codeBehind, Does.Contain("AnnotationDiag:"));
        }
    }

    [Test]
    public void WindowsBenchmark_ContainsFreshAndPooledLifecycleCases()
    {
        var benchmark = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "benchmarks",
            "InfiniteCanvas.Benchmarks",
            "AnnotationOverlayPoolingBenchmarks.Windows.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(benchmark, Does.Contain("RecreateDetachedStates"));
            Assert.That(benchmark, Does.Contain("ReuseDetachedStates"));
            Assert.That(benchmark, Does.Contain("ApartmentState.STA"));
            Assert.That(benchmark, Does.Contain("Children.Add"));
            Assert.That(benchmark, Does.Contain("Children.Remove"));
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