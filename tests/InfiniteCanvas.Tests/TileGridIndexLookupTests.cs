using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class TileGridIndexLookupTests
{
    [Test]
    public void TryGetTileIndex_ReturnsExpectedIndexAcrossRows()
    {
        var sceneBounds = new SpatialBounds(0, 0, 200, 150);

        var first = TileGridIndexLookup.TryGetTileIndex(10, 10, sceneBounds, 100, 50, 2, 6, out var firstIndex);
        var second = TileGridIndexLookup.TryGetTileIndex(150, 25, sceneBounds, 100, 50, 2, 6, out var secondIndex);
        var third = TileGridIndexLookup.TryGetTileIndex(25, 125, sceneBounds, 100, 50, 2, 6, out var thirdIndex);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(firstIndex, Is.EqualTo(0));
            Assert.That(second, Is.True);
            Assert.That(secondIndex, Is.EqualTo(1));
            Assert.That(third, Is.True);
            Assert.That(thirdIndex, Is.EqualTo(4));
        });
    }

    [Test]
    public void TryGetTileIndex_RespectsHalfOpenSceneBoundsAndRejectsInvalidInputs()
    {
        var sceneBounds = new SpatialBounds(0, 0, 200, 100);

        var rightEdge = TileGridIndexLookup.TryGetTileIndex(200, 10, sceneBounds, 100, 50, 2, 4, out _);
        var bottomEdge = TileGridIndexLookup.TryGetTileIndex(50, 100, sceneBounds, 100, 50, 2, 4, out _);
        var negative = TileGridIndexLookup.TryGetTileIndex(-1, 10, sceneBounds, 100, 50, 2, 4, out _);
        var invalidGrid = TileGridIndexLookup.TryGetTileIndex(10, 10, sceneBounds, 0, 50, 2, 4, out _);
        var invalidCount = TileGridIndexLookup.TryGetTileIndex(10, 10, sceneBounds, 100, 50, 2, 0, out _);

        Assert.Multiple(() =>
        {
            Assert.That(rightEdge, Is.False);
            Assert.That(bottomEdge, Is.False);
            Assert.That(negative, Is.False);
            Assert.That(invalidGrid, Is.False);
            Assert.That(invalidCount, Is.False);
        });
    }
}
