using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public sealed class RenderingDiagnosticsTests
{
    [Test]
    public void Generation_ReportsStageTimingsAndMipSampleCount()
    {
        var diagnostics = new RenderingDiagnostics();

        using (RenderingDiagnostics.Activate(diagnostics))
        {
            _ = SampleImageGenerator.GenerateMonochromeMipPixels(
                nativeWidth: 32,
                nativeHeight: 16,
                targetValue: 128,
                noise: 8,
                mipLevel: 1,
                seed: 1729,
                circleCount: 1,
                noiseSettings: SampleImageGenerator.NoiseSettings.Default);
        }

        var snapshot = diagnostics.Snapshot();
        var mip = snapshot.MipLevels[1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.GetStageDuration(RenderingStage.NativeNoiseGeneration), Is.GreaterThan(TimeSpan.Zero));
            Assert.That(snapshot.GetStageSampleCount(RenderingStage.NativeNoiseGeneration), Is.EqualTo(1));
            Assert.That(snapshot.GetStageDuration(RenderingStage.Gray8Normalization), Is.GreaterThan(TimeSpan.Zero));
            Assert.That(snapshot.GetStageSampleCount(RenderingStage.Gray8Normalization), Is.EqualTo(1));
            Assert.That(snapshot.GetStageDuration(RenderingStage.CircleRasterization), Is.GreaterThan(TimeSpan.Zero));
            Assert.That(mip.Requested, Is.EqualTo(0));
            Assert.That(mip.Generated, Is.EqualTo(1));
            Assert.That(mip.SampleCount, Is.EqualTo(128));
        }
    }

    [Test]
    public void Snapshot_RecordsOutcomeAndResidentPayloadBytesByMip()
    {
        var diagnostics = new RenderingDiagnostics();

        diagnostics.Record(RenderingDiagnosticOutcome.Requested, 2, sampleCount: 128, residentPayloadBytes: 128);
        diagnostics.Record(RenderingDiagnosticOutcome.Generated, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Reused, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.ResidentFallback, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Useful, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Stale, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Rejected, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Failed, 2);
        diagnostics.Record(RenderingDiagnosticOutcome.Evicted, 2);

        var mip = diagnostics.Snapshot().MipLevels[2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mip.Requested, Is.EqualTo(1));
            Assert.That(mip.Generated, Is.EqualTo(1));
            Assert.That(mip.Reused, Is.EqualTo(1));
            Assert.That(mip.ResidentFallback, Is.EqualTo(1));
            Assert.That(mip.Useful, Is.EqualTo(1));
            Assert.That(mip.Stale, Is.EqualTo(1));
            Assert.That(mip.Rejected, Is.EqualTo(1));
            Assert.That(mip.Failed, Is.EqualTo(1));
            Assert.That(mip.Evicted, Is.EqualTo(1));
            Assert.That(mip.SampleCount, Is.EqualTo(128));
            Assert.That(mip.ResidentPayloadBytes, Is.EqualTo(128));
        }
    }
}