namespace InfiniteCanvas.Rendering;

public static class DefectOverlaySampler
{
    public static byte ResolveDisplayValue(byte currentValue, SampleAnnotation annotation, double worldX, double worldY)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        return annotation.TryGetDefectValue(worldX, worldY, out var defectValue)
            ? defectValue
            : currentValue;
    }

    public static byte ResolveDisplayValue(byte currentValue, IEnumerable<SampleAnnotation> annotations, double worldX, double worldY)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var resolvedValue = currentValue;
        foreach (var annotation in annotations)
        {
            resolvedValue = ResolveDisplayValue(resolvedValue, annotation, worldX, worldY);
        }

        return resolvedValue;
    }
}
