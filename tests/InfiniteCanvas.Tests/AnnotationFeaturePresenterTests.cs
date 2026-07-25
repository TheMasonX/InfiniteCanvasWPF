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
            Id: "A-1",
            TileId: "tile-1",
            ObjectId: "object-1",
            Bounds: new SpatialBounds(0, 0, 10, 10),
            Color: new Bgra32Color(255, 0, 0, 0),
            Classification: "Crack",
            Features: new Dictionary<string, double>
            {
                ["Severity"] = 0.25,
                ["Confidence"] = 0.8,
                ["Area"] = 14.5
            },
            DefectPixelWidth: 1,
            DefectPixelHeight: 1,
            DefectPixels: new byte[] { 0 });

        var rows = AnnotationFeaturePresenter.BuildRows(annotation);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.Name), Is.EqualTo(new[] { "Area", "Confidence", "Severity" }));
            Assert.That(rows[1].Value, Is.EqualTo("80.0 %"));
            Assert.That(rows[2].Value, Is.EqualTo("25.0 %"));
            Assert.That(rows[0].Value, Is.EqualTo("14.5"));
        });
    }
}
