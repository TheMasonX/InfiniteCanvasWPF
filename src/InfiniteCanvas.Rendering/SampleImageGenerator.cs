using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

public static class SampleImageGenerator
{
    private static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];

    public static IReadOnlyList<SampleImageTile> GenerateSet(
        int imageCount = 32,
        int pixelWidth = 8192,
        int pixelHeight = 2048,
        byte targetValue = 128,
        byte noise = 8,
        int objectsPerTile = 16,
        int columns = 2,
        int seed = 1729)
    {
        if (imageCount <= 0 || pixelWidth <= 0 || pixelHeight <= 0 || objectsPerTile < 0 || columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageCount));
        }

        var tiles = new SampleImageTile[imageCount];

        for (var tileIndex = 0; tileIndex < imageCount; tileIndex++)
        {
            var tileId = $"TILE-{tileIndex + 1:D2}";
            var tileX = (tileIndex % columns) * (double)pixelWidth;
            var tileY = (tileIndex / columns) * (double)pixelHeight;
            var bounds = new SpatialBounds(tileX, tileY, pixelWidth, pixelHeight);
            var pixelSeed = unchecked(seed + (tileIndex * 104729));
            var annotationSeed = unchecked(seed + (tileIndex * 130363) + 7919);
            var annotations = GenerateAnnotations(
                tileId,
                bounds,
                objectsPerTile,
                new Random(annotationSeed));
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromePixels(pixelWidth, pixelHeight, targetValue, noise, pixelSeed),
                annotations);
        }

        return tiles;
    }

    public static byte[] GenerateMonochromePixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed = 1729)
    {
        return GenerateMonochromePixels(width, height, targetValue, noise, new Random(seed));
    }

    private static byte[] GenerateMonochromePixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        Random random)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var minimum = Math.Max(byte.MinValue, targetValue - noise);
        var maximum = Math.Min(byte.MaxValue, targetValue + noise);
        var range = maximum - minimum;
        var pixels = new byte[checked(width * height)];
        random.NextBytes(pixels);

        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(minimum + ((pixels[index] * range) / byte.MaxValue));
        }

        return pixels;
    }

    private static IReadOnlyList<SampleAnnotation> GenerateAnnotations(
        string tileId,
        SpatialBounds tileBounds,
        int count,
        Random random)
    {
        var annotations = new SampleAnnotation[count];

        for (var index = 0; index < count; index++)
        {
            var size = random.Next(70, 201);
            var localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - size));
            var localY = random.Next(0, Math.Max(1, (int)tileBounds.Height - size));
            var objectId = random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12");
            var color = new Bgra32Color(
                (byte)random.Next(48, 256),
                (byte)random.Next(48, 256),
                (byte)random.Next(48, 256),
                byte.MaxValue);

            annotations[index] = new SampleAnnotation(
                $"{tileId}-{objectId}",
                tileId,
                objectId,
                new SpatialBounds(tileBounds.X + localX, tileBounds.Y + localY, size, size),
                color,
                Classifications[random.Next(Classifications.Length)],
                new Dictionary<string, double>
                {
                    ["Confidence"] = Math.Round(0.75 + (random.NextDouble() * 0.249), 3),
                    ["Severity"] = Math.Round(random.NextDouble(), 3)
                });
        }

        return annotations;
    }
}