using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InfiniteCanvas.Controls;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.Windows.Tests;

/// <summary>
/// Consumer-host gate for ICW-316. A second host references only the canvas
/// library, implements the Core source interfaces, constructs the control,
/// and publishes a frame through the CanvasFrame boundary. This proves the
/// library is usable outside the app assembly and that the control loads
/// with its own resources (no host-defined brushes required).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class CanvasControlConsumerHostTests
{
    [Test]
    public void ConsumerHost_ConstructsControlAndPublishesFrame()
    {
        var control = new CanvasControl();
        control.SceneSource = new HostSceneSource();

        var raster = CreateFrozenRaster(64, 48);
        var frame = new CanvasFrame(
            raster,
            [new HostItem("a", new SpatialBounds(0, 0, 10, 10))],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 3,
            width: 64,
            height: 48,
            revision: 7);

        var published = false;
        control.FramePublished += (_, _) => published = true;

        control.PublishFrame(frame);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.True, "FramePublished must fire for host overlay composition.");
            Assert.That(control.ViewModel.VisibleItemCount, Is.EqualTo(1));
            Assert.That(control.ViewModel.TotalItemCount, Is.EqualTo(3));
            Assert.That(control.ViewModel.VisibleItems, Has.Count.EqualTo(1));
            Assert.That(control.ViewModel.VisibleItems[0].Id, Is.EqualTo("a"));
        });
    }

    [Test]
    public void ConsumerHost_SceneSourceDependencyProperty_AcceptsHostImplementation()
    {
        var control = new CanvasControl
        {
            SceneSource = new HostSceneSource()
        };

        Assert.Multiple(() =>
        {
            Assert.That(control.SceneSource, Is.Not.Null);
            Assert.That(control.SceneSource!.TotalItemCount, Is.EqualTo(3));
            Assert.That(control.SceneSource!.SceneBounds, Is.EqualTo(new SpatialBounds(0, 0, 100, 100)));
        });

        var visible = control.SceneSource!.QueryVisible(new SpatialBounds(0, 0, 100, 100));
        Assert.That(visible, Has.Count.EqualTo(1));
    }

    [Test]
    public void ConsumerHost_ControlLoadsWithSelfContainedResources()
    {
        // The control must construct without an application-level resource
        // dictionary (ICW-316): StaticResource lookups resolve inside the
        // control's own resources.
        var control = new CanvasControl();

        Assert.That(control, Is.Not.Null);
        Assert.That(control.IsLoadingVisible, Is.False);
    }

    private static BitmapSource CreateFrozenRaster(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed class HostItem : ICanvasItem
    {
        public HostItem(string id, SpatialBounds bounds)
        {
            Id = id;
            Bounds = bounds;
        }

        public string Id { get; }

        public SpatialBounds Bounds { get; }
    }

    private sealed class HostSceneSource : ICanvasSceneSource
    {
        public SpatialBounds SceneBounds { get; } = new(0, 0, 100, 100);

        public int TotalItemCount => 3;

#pragma warning disable CS0067 // Interface member the fake never raises.
        public event EventHandler? SceneChanged;
#pragma warning restore CS0067

        public IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport) =>
            [new HostItem("a", new SpatialBounds(0, 0, 10, 10))];

        public IReadOnlyList<ICanvasItem> QueryPoint(double worldX, double worldY) =>
            [];

        public bool TryReadResidentPixel(double worldX, double worldY, int mipLevel, out CanvasPixelSample sample)
        {
            sample = default;
            return false;
        }
    }
}
