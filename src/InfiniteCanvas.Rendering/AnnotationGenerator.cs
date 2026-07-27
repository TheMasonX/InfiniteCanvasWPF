using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

internal static class AnnotationGenerator
{
    public static IReadOnlyList<SampleAnnotation> GenerateAnnotations(
        string tileId,
        SpatialBounds tileBounds,
        int count,
        SampleImageGenerator.DeterministicRandom random,
        IReadOnlyList<SampleImageGenerator.DefectTemplate> defectTemplatePool)
    {
        var annotations = new SampleAnnotation[count];

        for (var index = 0; index < count; index++)
        {
            var classification = SampleImageGenerator.Classifications[random.Next(SampleImageGenerator.Classifications.Length)];
            var (aspectMin, aspectMax) = SampleImageGenerator.GetClassAspectRange(classification);
            var aspectRatio = aspectMin + (random.NextDouble() * (aspectMax - aspectMin));
            var width = random.Next(160, 561);
            var height = Math.Clamp((int)Math.Round(width / aspectRatio), 100, 620);
            var localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - width));
            var localY = random.Next(0, Math.Max(1, (int)tileBounds.Height - height));
            var objectId = random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12");
            var color = SampleImageGenerator.ClassificationColors[classification];
            var defectTemplate = defectTemplatePool[random.Next(defectTemplatePool.Count)];

            annotations[index] = new SampleAnnotation(
                $"{tileId}-{objectId}",
                tileId,
                objectId,
                new SpatialBounds(tileBounds.X + localX, tileBounds.Y + localY, width, height),
                color,
                classification,
                new Dictionary<string, double>
                {
                    ["Confidence"] = Math.Round(0.75 + (random.NextDouble() * 0.249), 3),
                    ["Severity"] = Math.Round(random.NextDouble(), 3)
                },
                defectTemplate.Width,
                defectTemplate.Height,
                defectTemplate.Pixels
#if WINDOWS
            )
            {
                DefectBitmap = defectTemplate.Bitmap
            };
#else
            );
#endif
        }

        return annotations;
    }
}
