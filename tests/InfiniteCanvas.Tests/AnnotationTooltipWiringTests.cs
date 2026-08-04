namespace InfiniteCanvas.Tests;

[TestFixture]
public class AnnotationTooltipWiringTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Test]
    public void MainWindow_UsesDeferredPresenterBackedTooltipSource()
    {
        var codeBehind = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "MainWindow.xaml.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(codeBehind, Does.Contain("ToolTip = new DeferredAnnotationToolTip(annotation)"));
            Assert.That(codeBehind, Does.Not.Contain("ToolTip = CreateAnnotationToolTip(annotation)"));
            Assert.That(codeBehind, Does.Not.Contain("annotation.Features[\"Confidence\"]"));
            Assert.That(codeBehind, Does.Not.Contain("annotation.Features[\"Severity\"]"));
            Assert.That(codeBehind, Does.Not.Contain("private static ToolTip CreateAnnotationToolTip"));
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