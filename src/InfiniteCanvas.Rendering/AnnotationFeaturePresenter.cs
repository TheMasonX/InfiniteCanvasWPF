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
            // Plain double formatting. The feature dictionary has no per-key
            // schema, so it cannot encode which values are percents. Do not
            // format values in the 0..1 range as percents (ICW-206).
            return d.ToString(CultureInfo.InvariantCulture);
        }
        else return value.ToString() ?? "";
    }
}
