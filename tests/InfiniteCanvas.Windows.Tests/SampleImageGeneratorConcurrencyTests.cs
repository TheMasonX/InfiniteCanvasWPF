using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Windows.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class SampleImageGeneratorConcurrencyTests
{
    [Test]
    public async Task ConcurrentGdiPlusGeneration_CompletesWithoutNativeFailure()
    {
        var jobs = Enumerable.Range(0, 8)
            .Select(worker => Task.Run(() =>
            {
                for (var iteration = 0; iteration < 125; iteration++)
                {
                    var pixels = SampleImageGenerator.GenerateMonochromeMipPixels(
                        128,
                        64,
                        targetValue: 128,
                        noise: 0,
                        mipLevel: 0,
                        seed: worker * 1000 + iteration,
                        circleCount: 6,
                        tileLabel: $"TILE-{worker:D2}");

                    Assert.That(pixels, Has.Length.EqualTo(128 * 64));
                    Assert.That(pixels.Any(value => value < 128), Is.True);
                }
            }))
            .ToArray();

        await Task.WhenAll(jobs);
    }
}