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

        for (int index = 0; index < count; index++)
        {
            string classification = SampleImageGenerator.Classifications[random.Next(SampleImageGenerator.Classifications.Length)];
            (double aspectMin, double aspectMax) = SampleImageGenerator.GetClassAspectRange(classification);
            double aspectRatio = aspectMin + (random.NextDouble() * (aspectMax - aspectMin));
            int width = random.Next(160, 561);
            int height = Math.Clamp((int)Math.Round(width / aspectRatio), 100, 620);
            int localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - width));
            int localY = random.Next(0, Math.Max(1, (int)tileBounds.Height - height));
            string objectId = random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12");
            Bgra32Color color = SampleImageGenerator.ClassificationColors[classification];
            SampleImageGenerator.DefectTemplate defectTemplate = defectTemplatePool[random.Next(defectTemplatePool.Count)];
            double confidence = Math.Round(0.75 + (random.NextDouble() * 0.249), 3);
            double severity = Math.Round(random.NextDouble(), 3);

            annotations[index] = new SampleAnnotation(
                $"{tileId}-{objectId}",
                tileId,
                objectId,
                new SpatialBounds(tileBounds.X + localX, tileBounds.Y + localY, width, height),
                color,
                classification,
                () => new Dictionary<string, object>
                {
                    ["ID"] = index,
                    ["Class"] = classification,
                    ["Confidence"] = confidence,
                    ["Severity"] = severity,
                    ["Area"] = width * height,
                    ["Width"] = width,
                    ["Height"] = height,
                    ["AspectRatio"] = Math.Round(aspectRatio, 3),
                    ["Left"] = tileBounds.X + localX,
                    ["Top"] = tileBounds.Y + localY,
                    ["Right"] = tileBounds.X + localX + width,
                    ["Bottom"] = tileBounds.Y + localY + height
                },
                defectTemplate.Width,
                defectTemplate.Height,
                defectTemplate.Pixels,
                new AnnotationMetrics(confidence, severity));
        }

        return annotations;
    }
}
