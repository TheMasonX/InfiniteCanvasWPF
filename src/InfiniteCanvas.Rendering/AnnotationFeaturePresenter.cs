using System.Globalization;

namespace InfiniteCanvas.Rendering;

public static class AnnotationFeaturePresenter
{
    public static IReadOnlyList<FeatureDisplayItem> BuildRows(SampleAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        return annotation.Features
            .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new FeatureDisplayItem(item.Key, FormatFeatureValue(item.Value)))
            .ToArray();
    }

    public static string BuildTooltipContent(SampleAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        var confidence = annotation.Features.TryGetValue("Confidence", out var confidenceValue) ? confidenceValue : 0;
        var severity = annotation.Features.TryGetValue("Severity", out var severityValue) ? severityValue : 0;

        return string.Join(
            Environment.NewLine,
            annotation.Id,
            annotation.Classification,
            $"Confidence {FormatFeatureValue(confidence)}  |  Severity {FormatFeatureValue(severity)}");
    }

    private static string FormatFeatureValue(double value)
    {
        return value is <= 1.0 and >= 0.0
            ? value.ToString("P1", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
