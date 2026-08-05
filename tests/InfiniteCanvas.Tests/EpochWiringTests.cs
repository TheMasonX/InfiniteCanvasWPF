using System.Text.RegularExpressions;

namespace InfiniteCanvas.Tests;

/// <summary>
/// Guards the RenderRequestTracker wiring in the render pipeline (ICW-078,
/// ICW-100, ICW-323). The primitive tests prove the tracker works; this test
/// proves the host still calls BeginRequest, IsCurrent, and Advance inside
/// RenderFrameAsync. The 2026-07-26 epoch-guard revert slipped exactly because
/// nothing observed the wiring. Test-only, no production risk.
/// </summary>
[TestFixture]
public class EpochWiringTests
{
    private static readonly string MainWindowCodeBehind = File.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "InfiniteCanvas.App",
        "MainWindow.xaml.cs"));

    [Test]
    public void RenderFrameAsync_KeepsBeginIsCurrentAdvanceWired()
    {
        var renderFrameBody = ExtractMethodBody("RenderFrameAsync");

        Assert.That(renderFrameBody, Is.Not.EqualTo("method not found"), "RenderFrameAsync must exist in MainWindow.xaml.cs.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(renderFrameBody, Does.Contain("_renderRequestTracker.BeginRequest()"),
                "RenderFrameAsync must begin a request epoch (ICW-078).");
            Assert.That(renderFrameBody, Does.Contain("_renderRequestTracker.IsCurrent(requestVersion)"),
                "RenderFrameAsync must reject stale frames via IsCurrent (ICW-078).");
            Assert.That(renderFrameBody, Does.Contain("_renderRequestTracker.Advance()"),
                "RenderFrameAsync must advance the epoch after a successful publish (ICW-078).");
            Assert.That(renderFrameBody, Does.Contain("if (!_renderRequestTracker.IsCurrent(requestVersion))"),
                "The stale-frame rejection must precede publication.");
        }
    }

    private static string ExtractMethodBody(string methodName)
    {
        var startPattern = new Regex($@"(private|public|internal|protected)\s+(async\s+)?[\w<>,\[\]\.]+\s+{methodName}\s*\(");
        var start = startPattern.Match(MainWindowCodeBehind);
        if (!start.Success)
        {
            return "method not found";
        }

        var braceStart = MainWindowCodeBehind.IndexOf('{', start.Index);
        if (braceStart < 0)
        {
            return "method not found";
        }

        var depth = 0;
        var end = -1;
        for (var i = braceStart; i < MainWindowCodeBehind.Length; i++)
        {
            if (MainWindowCodeBehind[i] == '{')
            {
                depth++;
            }
            else if (MainWindowCodeBehind[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end < 0)
        {
            return "method not found";
        }

        return MainWindowCodeBehind[braceStart..(end + 1)];
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
