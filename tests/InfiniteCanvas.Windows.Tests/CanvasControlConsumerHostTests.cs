using System.Windows;
using System.Windows.Controls;
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
    public void ConsumerHost_StaleFrameRevision_IsDiscarded()
    {
        // ICW-329: the canvas discards a frame whose revision is older than
        // the last one displayed. The revision is the host render-request
        // version; an out-of-order publish must never overwrite newer state.
        var control = new CanvasControl();
        control.SceneSource = new HostSceneSource();

        var raster1 = CreateFrozenRaster(64, 48);
        var frame1 = new CanvasFrame(
            raster1,
            [new HostItem("a", new SpatialBounds(0, 0, 10, 10))],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 3,
            width: 64,
            height: 48,
            revision: 7);

        var raster2 = CreateFrozenRaster(64, 48);
        var frame2 = new CanvasFrame(
            raster2,
            [new HostItem("b", new SpatialBounds(0, 0, 10, 10))],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 3,
            width: 64,
            height: 48,
            revision: 5);

        var publishedCount = 0;
        control.FramePublished += (_, _) => publishedCount++;

        control.PublishFrame(frame1);
        control.PublishFrame(frame2);

        Assert.Multiple(() =>
        {
            Assert.That(publishedCount, Is.EqualTo(1),
                "An older-revision frame must be discarded and not raise FramePublished.");
            Assert.That(control.ViewModel.VisibleItems, Has.Count.EqualTo(1));
            Assert.That(control.ViewModel.VisibleItems[0].Id, Is.EqualTo("a"),
                "The stale frame must not overwrite the newer frame state.");
        });
    }

    [Test]
    public void ConsumerHost_StaleSemanticIdentity_IsDiscardedEvenWithNewerRenderSequence()
    {
        var control = new CanvasControl
        {
            SceneSource = new HostSceneSource()
        };
        var currentIdentity = new CanvasFrameIdentity(
            "session-a",
            sceneRevision: 2,
            new CanvasLayerRevisionVector(2, 2, 2, 2, 2),
            displayRevision: 2,
            selectionRevision: 2,
            renderSequence: 7);
        var staleIdentity = new CanvasFrameIdentity(
            "session-a",
            sceneRevision: 1,
            new CanvasLayerRevisionVector(1, 1, 1, 1, 1),
            displayRevision: 1,
            selectionRevision: 1,
            renderSequence: 8);
        var firstFrame = new CanvasFrame(
            CreateFrozenRaster(64, 48),
            [new HostItem("current", new SpatialBounds(0, 0, 10, 10))],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 1,
            width: 64,
            height: 48,
            revision: 7,
            identity: currentIdentity);
        var staleFrame = new CanvasFrame(
            CreateFrozenRaster(64, 48),
            [new HostItem("stale", new SpatialBounds(0, 0, 10, 10))],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 1,
            width: 64,
            height: 48,
            revision: 8,
            identity: staleIdentity);

        var publishedCount = 0;
        var layerPublishingCount = 0;
        control.FrameLayersPublishing += (_, _) => layerPublishingCount++;
        control.FramePublished += (_, _) => publishedCount++;
        control.PublishFrame(firstFrame);
        control.PublishFrame(staleFrame);

        Assert.Multiple(() =>
        {
            Assert.That(layerPublishingCount, Is.EqualTo(1));
            Assert.That(publishedCount, Is.EqualTo(1));
            Assert.That(control.ViewModel.VisibleItems[0].Id, Is.EqualTo("current"));
        });
    }

    [Test]
    public void ConsumerHost_AcceptsNewSourceSessionWhenRenderSequenceResets()
    {
        var control = new CanvasControl();
        var firstFrame = CreateFrame(
            new HostItem("old", new SpatialBounds(0, 0, 10, 10)),
            revision: 7,
            identity: new CanvasFrameIdentity(
                "session-a",
                4,
                new CanvasLayerRevisionVector(4, 4, 4, 4, 4),
                4,
                4,
                7));
        var replacementFrame = CreateFrame(
            new HostItem("new", new SpatialBounds(0, 0, 10, 10)),
            revision: 0,
            identity: new CanvasFrameIdentity(
                "session-b",
                0,
                default,
                0,
                0,
                0));

        control.PublishFrame(firstFrame);
        control.PublishFrame(replacementFrame);

        Assert.That(control.ViewModel.VisibleItems[0].Id, Is.EqualTo("new"));
    }

    [Test]
    public void ConsumerHost_AcceptedFrameCarriesOrderedLayerPlan()
    {
        var identity = new CanvasFrameIdentity(
            "session-a",
            1,
            new CanvasLayerRevisionVector(3, 4, 5, 6, 7),
            8,
            9,
            10);
        var frame = new CanvasFrame(
            CreateFrozenRaster(64, 48),
            [],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 0,
            totalItemCount: 0,
            width: 64,
            height: 48,
            revision: 10,
            identity: identity,
            layerPlan: new CanvasLayerPlan(
            [
                new(CanvasLayerKind.Raster, true, 10),
                new(CanvasLayerKind.BackgroundMaterial, true, 3),
                new(CanvasLayerKind.DefectImagery, false, 4),
                new(CanvasLayerKind.TileGrid, true, 5),
                new(CanvasLayerKind.Annotations, true, 6),
                new(CanvasLayerKind.Labels, false, 6),
                new(CanvasLayerKind.Selection, false, 9),
                new(CanvasLayerKind.Pixelometer, true, 7)
            ]));

        var control = new CanvasControl();
        control.PublishFrame(frame);

        Assert.That(
            frame.LayerPlan.Layers.Select(layer => layer.Kind),
            Is.EqualTo(Enum.GetValues<CanvasLayerKind>()));
        Assert.That(frame.LayerPlan.IsVisible(CanvasLayerKind.DefectImagery), Is.False);
    }

    [Test]
    public void ConsumerHost_RejectsMismatchedIntegerAndSemanticRenderSequence()
    {
        var identity = new CanvasFrameIdentity(
            "session-a",
            1,
            default,
            0,
            0,
            renderSequence: 9);

        Assert.Throws<ArgumentException>(() => new CanvasFrame(
            CreateFrozenRaster(64, 48),
            [],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 0,
            totalItemCount: 0,
            width: 64,
            height: 48,
            revision: 8,
            identity: identity));
    }

    [Test]
    public void ConsumerHost_RejectsUnfrozenRaster()
    {
        var raster = new WriteableBitmap(64, 48, 96, 96, PixelFormats.Bgra32, null);

        Assert.Throws<ArgumentException>(() => new CanvasFrame(
            raster,
            [],
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 0,
            totalItemCount: 0,
            width: 64,
            height: 48));
    }

    [Test]
    public void ConsumerHost_FrameOwnsItemSequence()
    {
        var control = new CanvasControl();
        var items = new List<ICanvasItem>
        {
            new HostItem("a", new SpatialBounds(0, 0, 10, 10))
        };
        var frame = new CanvasFrame(
            CreateFrozenRaster(64, 48),
            items,
            new SpatialBounds(0, 0, 100, 100),
            visibleItemCount: 1,
            totalItemCount: 1,
            width: 64,
            height: 48,
            revision: 1);

        items.Clear();
        control.PublishFrame(frame);

        Assert.That(frame.Items, Has.Count.EqualTo(1));
        Assert.That(control.ViewModel.VisibleItems, Has.Count.EqualTo(1));
    }

    [Test]
    public void ConsumerHost_ControlOwnsTooltipRegistrationAndClearsItOnNextFrame()
    {
        var control = new CanvasControl();
        var visual = new Border();

        control.RegisterItemVisual(visual, "tooltip text");
        var firstTooltip = visual.ToolTip;

        control.PublishFrame(CreateFrame(new HostItem("plain", new SpatialBounds(0, 0, 10, 10)), 1));

        Assert.Multiple(() =>
        {
            Assert.That(firstTooltip, Is.Not.Null);
            Assert.That(firstTooltip!.ToString(), Is.EqualTo("tooltip text"));
            Assert.That(visual.ToolTip, Is.Null);
        });
    }

    [Test]
    public void ConsumerHost_ReusesTooltipWrapperForRetainedVisual()
    {
        var control = new CanvasControl();
        var visual = new Border();

        control.RegisterItemVisual(visual, "tooltip text");
        var firstTooltip = visual.ToolTip;

        control.PublishFrame(CreateFrame(new HostItem("plain", new SpatialBounds(0, 0, 10, 10)), 1));
        control.RegisterItemVisual(visual, "tooltip text");

        Assert.That(visual.ToolTip, Is.SameAs(firstTooltip));
    }

    [Test]
    public void ConsumerHost_DetachingFrameShell_ClearsRegisteredTooltips()
    {
        var control = new CanvasControl();
        var visual = new Border();

        control.PublishFrame(CreateFrame(new HostItem("plain", new SpatialBounds(0, 0, 10, 10)), 1));
        control.RegisterItemVisual(visual, "tooltip text");

        control.DetachFrameShell();

        Assert.That(visual.ToolTip, Is.Null);
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

    [Test]
    public void ConsumerHost_SelectsItemAtViewportPointAndClearsOnEmptySpace()
    {
        var control = new CanvasControl
        {
            SceneSource = new SelectableHostSceneSource()
        };
        control.SetSceneBounds(new SpatialBounds(0, 0, 100, 100));
        control.SetViewportSize(100, 100);

        var changes = new List<ICanvasItem?>();
        control.SelectionChanged += (_, args) => changes.Add(args.SelectedItem);

        control.SelectAtViewportPoint(new Point(25, 25));
        control.SelectAtViewportPoint(new Point(90, 90));

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedItem, Is.Null);
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[0]!.Id, Is.EqualTo("selected"));
            Assert.That(changes[1], Is.Null);
        });
    }

    private static BitmapSource CreateFrozenRaster(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.Freeze();
        return bitmap;
    }

    private static CanvasFrame CreateFrame(ICanvasItem item, int revision, CanvasFrameIdentity? identity = null) => new(
        CreateFrozenRaster(64, 48),
        [item],
        new SpatialBounds(0, 0, 100, 100),
        visibleItemCount: 1,
        totalItemCount: 1,
        width: 64,
        height: 48,
        revision: revision,
        identity: identity);

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

        public CanvasFrameIdentity Identity { get; } = CanvasFrameIdentity.Default();

        public int TotalItemCount => 3;

#pragma warning disable CS0067 // Interface member the fake never raises.
        public event EventHandler<CanvasSceneChangedEventArgs>? SceneChanged;
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

    private sealed class SelectableHostSceneSource : ICanvasSceneSource
    {
        private readonly HostItem _item = new("selected", new SpatialBounds(10, 10, 30, 30));

        public SpatialBounds SceneBounds { get; } = new(0, 0, 100, 100);

        public CanvasFrameIdentity Identity { get; } = CanvasFrameIdentity.Default();

        public int TotalItemCount => 1;

#pragma warning disable CS0067
        public event EventHandler<CanvasSceneChangedEventArgs>? SceneChanged;
#pragma warning restore CS0067

        public IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport) => [_item];

        public IReadOnlyList<ICanvasItem> QueryPoint(double worldX, double worldY) => [_item];

        public bool TryReadResidentPixel(double worldX, double worldY, int mipLevel, out CanvasPixelSample sample)
        {
            sample = default;
            return false;
        }
    }
}
