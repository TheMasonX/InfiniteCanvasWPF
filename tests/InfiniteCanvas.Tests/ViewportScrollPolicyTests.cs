using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class ViewportScrollPolicyTests
{
    [Test]
    public void ComputeContentSize_ReturnsAtLeastViewportSize()
    {
        var camera = new CameraSnapshot(2.0, 2.0, 64, 64);

        var (contentWidth, contentHeight) = ViewportScrollPolicy.ComputeContentSize(
            viewportWidth: 800,
            viewportHeight: 600,
            sceneWidth: 200,
            sceneHeight: 100,
            camera: camera);

        Assert.Multiple(() =>
        {
            Assert.That(contentWidth, Is.GreaterThanOrEqualTo(800));
            Assert.That(contentHeight, Is.GreaterThanOrEqualTo(600));
        });
    }

    [Test]
    public void ComputeScrollOffsets_MapsCameraOffsetIntoScrollableRange()
    {
        var camera = new CameraSnapshot(1.25, 1.25, -200, -120);

        var (horizontalOffset, verticalOffset) = ViewportScrollPolicy.ComputeScrollOffsets(
            viewportWidth: 800,
            viewportHeight: 600,
            contentWidth: 1600,
            contentHeight: 1200,
            camera: camera);

        Assert.Multiple(() =>
        {
            Assert.That(horizontalOffset, Is.EqualTo(100));
            Assert.That(verticalOffset, Is.EqualTo(60));
        });
    }
}
