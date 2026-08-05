namespace InfiniteCanvas.Tests;

/// <summary>
/// Zero-reference gate for ICW-312. The canvas control and view model must
/// stay free of application and rendering types so the canvas can move to a
/// separate library and another app can supply its own data sources
/// (ADR-0007).
/// </summary>
[TestFixture]
public sealed class CanvasBoundaryZeroReferenceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string[] ForbiddenTokens =
    [
        "SampleAnnotation",
        "SampleImageTile",
        "LiveSpatialIndexService",
        "InfiniteCanvas.Spatial",
        "InfiniteCanvas.Rendering"
    ];

    private static readonly (string RelativePath, string Label)[] BoundaryFiles =
    [
        (Path.Combine("src", "InfiniteCanvas.App", "Controls", "CanvasControl.xaml.cs"), "CanvasControl.xaml.cs"),
        (Path.Combine("src", "InfiniteCanvas.ViewModels", "CanvasViewModel.cs"), "CanvasViewModel.cs")
    ];

    [TestCaseSource(nameof(BoundaryFiles))]
    public void BoundaryFile_HasNoApplicationOrRenderingReferences((string RelativePath, string Label) file)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, file.RelativePath));
        var matches = ForbiddenTokens.Where(token => source.Contains(token, StringComparison.Ordinal)).ToArray();

        Assert.That(matches, Is.Empty,
            $"{file.Label} must not reference application or rendering types. Found: {string.Join(", ", matches)}");
    }

    [Test]
    public void CanvasControl_ExposesSingleItemQueryAuthority()
    {
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.App", "Controls", "CanvasControl.xaml.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source, Does.Contain("SceneSourceProperty"),
                "CanvasControl must expose the SceneSource dependency property.");
            Assert.That(source, Does.Contain("public ICanvasSceneSource? SceneSource"),
                "CanvasControl must expose the typed SceneSource accessor.");
            Assert.That(source, Does.Not.Contain("SpatialQuerySourceProperty"),
                "The duplicate spatial-query source dependency property must be gone (ICW-316A F-001).");
        }
    }

    [Test]
    public void ViewModelsProject_DoesNotReferenceSpatialAssembly()
    {
        var project = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "InfiniteCanvas.ViewModels", "InfiniteCanvas.ViewModels.csproj"));

        Assert.That(project, Does.Not.Contain("InfiniteCanvas.Spatial"),
            "The view model project must not reference the spatial assembly (council cleanup).");
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
