using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniteCanvas.Rendering;

public static class TileCacheDiagnosticsExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(TileCacheDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static async Task WriteAsync(
        string filePath,
        TileCacheDiagnosticsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = Serialize(snapshot);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}