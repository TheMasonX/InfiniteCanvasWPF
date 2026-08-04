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

    private static string FormatFeatureValue(object value)
    {
        if (value is null)
        {
            return "";
        }
        else if (value is double d)
        {
            return d.ToString("F1", CultureInfo.InvariantCulture);
        }
        else return value.ToString() ?? "";
    }
}
