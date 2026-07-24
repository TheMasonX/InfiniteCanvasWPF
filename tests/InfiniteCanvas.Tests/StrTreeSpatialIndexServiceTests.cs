using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class StrTreeSpatialIndexServiceTests
{
    [Test]
    public void Query_ReturnsOnlyIntersectingItems()
    {
        var items = new[]
        {
            new SpatialRecord<string>("inside", new SpatialBounds(5, 5, 2, 2), "inside"),
            new SpatialRecord<string>("overlapping", new SpatialBounds(9, 9, 5, 5), "overlapping"),
            new SpatialRecord<string>("outside", new SpatialBounds(20, 20, 2, 2), "outside")
        };
        var index = new StrTreeSpatialIndexService<SpatialRecord<string>>(items);

        var results = index.Query(new SpatialBounds(0, 0, 10, 10));

        Assert.That(results.Select(item => item.Id), Is.EquivalentTo(new[] { "inside", "overlapping" }));
    }
}
