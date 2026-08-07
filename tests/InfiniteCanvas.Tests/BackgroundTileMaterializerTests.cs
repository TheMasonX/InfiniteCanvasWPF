using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class BackgroundTileMaterializerTests
{
    [Test]
    public void Request_CoalescesEqualVariantAndCachesValidatedPayload()
    {
        var source = new CountingSource();
        using var coordinator = new TileWorkCoordinator();
        using var materializer = new BackgroundTileMaterializer(source, coordinator, maxBytes: 64);
        var request = CreateRequest("source", "tile", 1, mipLevel: 1);
        var completed = new ManualResetEventSlim();

        Assert.That(materializer.Request(request, new object(), CancellationToken.None, _ => completed.Set()), Is.True);
        Assert.That(materializer.Request(request, new object(), CancellationToken.None), Is.True);
        Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(materializer.TryGetResident(request, out var payload), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.RequestCount, Is.EqualTo(1));
            Assert.That(payload.Width, Is.EqualTo(4));
            Assert.That(payload.Height, Is.EqualTo(2));
            Assert.That(materializer.UsedBytes, Is.EqualTo(8));
        }
    }

    [Test]
    public void AdvanceScene_DiscardsCompletionFromPreviousScene()
    {
        var source = new BlockingSource();
        using var coordinator = new TileWorkCoordinator();
        using var materializer = new BackgroundTileMaterializer(source, coordinator, maxBytes: 64);
        var request = CreateRequest("source", "tile", 1, mipLevel: 0);

        Assert.That(materializer.Request(request, new object(), CancellationToken.None), Is.True);
        Assert.That(source.Started.Wait(TimeSpan.FromSeconds(2)), Is.True);
        materializer.AdvanceScene();
        source.Release.Set();

        Assert.That(SpinWait.SpinUntil(() => coordinator.GetCounters().PendingCount == 0, TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(materializer.TryGetResident(request, out _), Is.False);
        Assert.That(materializer.UsedBytes, Is.Zero);
    }

    [Test]
    public void Admission_UsesVariantByteCostAndPreservesPinnedVariant()
    {
        var source = new CountingSource();
        using var coordinator = new TileWorkCoordinator();
        using var materializer = new BackgroundTileMaterializer(source, coordinator, maxBytes: 40);
        var mip0 = CreateRequest("source", "tile", 1, mipLevel: 0);
        var mip1 = CreateRequest("source", "tile", 1, mipLevel: 1);
        materializer.SetPinnedKeys([mip0.CacheKey]);

        Assert.That(materializer.Request(mip0, new object(), CancellationToken.None), Is.True);
        Assert.That(materializer.Request(mip1, new object(), CancellationToken.None), Is.True);
        Assert.That(SpinWait.SpinUntil(() => materializer.ResidentCount == 2, TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(materializer.Request(CreateRequest("source", "other", 1, 0), new object(), CancellationToken.None), Is.False);
        Assert.That(materializer.TryGetResident(mip0, out _), Is.True);
    }

    [Test]
    public void Request_RejectsPayloadForDifferentCacheKey()
    {
        var source = new MismatchedSource();
        using var coordinator = new TileWorkCoordinator();
        using var materializer = new BackgroundTileMaterializer(source, coordinator, maxBytes: 64);
        var request = CreateRequest("source", "tile", 1, mipLevel: 1);
        var failed = new ManualResetEventSlim();

        Assert.That(materializer.Request(request, new object(), CancellationToken.None, onFailed: _ => failed.Set()), Is.True);
        Assert.That(failed.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(materializer.TryGetResident(request, out _), Is.False);
        Assert.That(materializer.UsedBytes, Is.Zero);
    }

    private static BackgroundTileRequest CreateRequest(string sourceId, string tileId, long revision, int mipLevel) =>
        new(new BackgroundTileDescriptor(sourceId, tileId, revision, new SpatialBounds(0, 0, 8, 4), 8, 4), mipLevel);

    private sealed class CountingSource : IBackgroundTileSource
    {
        public int RequestCount;

        public ValueTask<BackgroundTilePayload> ResolveAsync(BackgroundTileRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref RequestCount);
            var (width, height) = request.CanonicalDimensions;
            return ValueTask.FromResult(new BackgroundTilePayload(request, new byte[width * height]));
        }
    }

    private sealed class BlockingSource : IBackgroundTileSource
    {
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public async ValueTask<BackgroundTilePayload> ResolveAsync(BackgroundTileRequest request, CancellationToken cancellationToken = default)
        {
            Started.Set();
            await Task.Run(Release.Wait, cancellationToken);
            var (width, height) = request.CanonicalDimensions;
            return new BackgroundTilePayload(request, new byte[width * height]);
        }
    }

    private sealed class MismatchedSource : IBackgroundTileSource
    {
        public ValueTask<BackgroundTilePayload> ResolveAsync(BackgroundTileRequest request, CancellationToken cancellationToken = default)
        {
            var mismatchedRequest = CreateRequest("other-source", request.Descriptor.TileId, request.Descriptor.ContentRevision, request.MipLevel);
            var (width, height) = mismatchedRequest.CanonicalDimensions;
            return ValueTask.FromResult(new BackgroundTilePayload(mismatchedRequest, new byte[width * height]));
        }
    }
}