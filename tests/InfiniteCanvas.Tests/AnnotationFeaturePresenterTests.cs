using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class AnnotationFeaturePresenterTests
{
    [Test]
    public void BuildFeatureRows_UsesTypedFeatureValuesAndStableOrdering()
    {
        var annotation = new SampleAnnotation(
            id: "A-1",
            tileId: "tile-1",
            objectId: "object-1",
            bounds: new SpatialBounds(0, 0, 10, 10),
            color: new Bgra32Color(255, 0, 0, 0),
            classification: "Crack",
            features: () => new Dictionary<string, object>
            {
                ["Severity"] = 0.25,
                ["Confidence"] = 0.8,
                ["Area"] = 14.5
            },
            defectPixelWidth: 1,
            defectPixelHeight: 1,
            defectPixels: new byte[] { 0 },
            metrics: new AnnotationMetrics(0.8, 0.25));

        var rows = AnnotationFeaturePresenter.BuildRows(annotation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.Name), Is.EqualTo(new[] { "Area", "Confidence", "Severity" }));
            // Feature values are formatted as plain doubles. The feature dict
            // has no schema to mark Confidence and Severity as percents, so no
            // percent encoding is applied (ICW-206).
            Assert.That(rows[0].Value, Is.EqualTo("14.5"));
            Assert.That(rows[1].Value, Is.EqualTo("0.8"));
            Assert.That(rows[2].Value, Is.EqualTo("0.25"));
        }
    }

    [Test]
    public void DeferredAnnotationToolTip_FormatsWhenContentIsRequested()
    {
        var annotation = new SampleAnnotation(
            id: "A-2",
            tileId: "tile-1",
            objectId: "object-2",
            bounds: new SpatialBounds(0, 0, 10, 10),
            color: new Bgra32Color(255, 0, 0, 0),
            classification: "Dent",
            features: () => new Dictionary<string, object>
            {
                ["Confidence"] = 0.9,
                ["Severity"] = 0.4
            },
            defectPixelWidth: 1,
            defectPixelHeight: 1,
            defectPixels: new byte[] { 0 },
            metrics: new AnnotationMetrics(0.9, 0.4));
        var deferredToolTip = new DeferredAnnotationToolTip(annotation);

        Assert.That(deferredToolTip.ToString(), Is.EqualTo(AnnotationFeaturePresenter.BuildTooltipContent(annotation)));
    }

    [Test]
    public void DeferredAnnotationToolTip_UsesDefaultsWhenKnownFeaturesAreMissing()
    {
        var annotation = new SampleAnnotation(
            id: "A-3",
            tileId: "tile-1",
            objectId: "object-3",
            bounds: new SpatialBounds(0, 0, 10, 10),
            color: new Bgra32Color(255, 0, 0, 0),
            classification: "Dent",
            features: () => new Dictionary<string, object>(),
            defectPixelWidth: 1,
            defectPixelHeight: 1,
            defectPixels: [0]);

        Assert.DoesNotThrow(() => new DeferredAnnotationToolTip(annotation).ToString());
    }
}
