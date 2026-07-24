using InfiniteCanvas.Core;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace InfiniteCanvas.Rendering;

public static class SampleImageGenerator
{
    private static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];
    private static readonly IReadOnlyDictionary<string, Bgra32Color> ClassificationColors = new Dictionary<string, Bgra32Color>
    {
        ["Scratch"] = new(60, 90, 245, 255),
        ["Inclusion"] = new(70, 205, 255, 255),
        ["Stain"] = new(120, 220, 120, 255),
        ["Edge defect"] = new(90, 160, 255, 255)
    };
    private readonly record struct DefectTemplate(int Width, int Height, byte[] Pixels);

    public static IReadOnlyList<SampleImageTile> GenerateSet(
        int imageCount = 64,
        int pixelWidth = 8192,
        int pixelHeight = 2048,
        byte targetValue = 128,
        byte noise = 8,
        int objectsPerTile = 16,
        int columns = 2,
        int seed = 1729,
        int? rows = null,
        int defectPoolSize = 64)
    {
        if (imageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageCount));
        }

        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        }

        if (objectsPerTile < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectsPerTile));
        }

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (defectPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defectPoolSize));
        }

        if (rows is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        var rowCount = rows ?? Math.Max(1, (int)Math.Ceiling(imageCount / (double)columns));
        var tileCount = rows.HasValue ? checked(columns * rowCount) : imageCount;
        var poolSeed = unchecked(seed + 48611);
        var poolRandom = new Random(poolSeed);
        var defectTemplatePool = BuildDefectTemplatePool(defectPoolSize, poolRandom);

        var tiles = new SampleImageTile[tileCount];

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
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
                new Random(annotationSeed),
                defectTemplatePool);
#if WINDOWS
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromeBitmap(pixelWidth, pixelHeight, targetValue, noise, pixelSeed),
                annotations,
                targetValue);
#else
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromePixels(pixelWidth, pixelHeight, targetValue, noise, pixelSeed),
                annotations,
                targetValue);
#endif
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

        _ = noise;
        _ = random;
        var pixels = new byte[checked(width * height)];
        Array.Fill(pixels, targetValue);

        return pixels;
    }

    private static IReadOnlyList<SampleAnnotation> GenerateAnnotations(
        string tileId,
        SpatialBounds tileBounds,
        int count,
        Random random,
        IReadOnlyList<DefectTemplate> defectTemplatePool)
    {
        var annotations = new SampleAnnotation[count];

        for (var index = 0; index < count; index++)
        {
            var classification = Classifications[random.Next(Classifications.Length)];
            var (aspectMin, aspectMax) = GetClassAspectRange(classification);
            var aspectRatio = aspectMin + (random.NextDouble() * (aspectMax - aspectMin));
            var width = random.Next(160, 561);
            var height = Math.Clamp((int)Math.Round(width / aspectRatio), 100, 620);
            var localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - width));
            var localY = random.Next(0, Math.Max(1, (int)tileBounds.Height - height));
            var objectId = random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12");
            var color = ClassificationColors[classification];
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
                defectTemplate.Pixels);
        }

        return annotations;
    }

    private static (double Min, double Max) GetClassAspectRange(string classification)
    {
        return classification switch
        {
            "Scratch" => (2.0, 4.4),
            "Inclusion" => (0.7, 1.6),
            "Stain" => (0.6, 1.8),
            "Edge defect" => (1.6, 3.2),
            _ => (0.8, 2.0)
        };
    }

    private static IReadOnlyList<DefectTemplate> BuildDefectTemplatePool(
        int count,
        Random random)
    {
        var pool = new DefectTemplate[count];
        for (var index = 0; index < count; index++)
        {
            var aspect = 0.45 + (random.NextDouble() * 1.95);
            var templateWidth = random.Next(156, 276);
            var templateHeight = Math.Clamp((int)Math.Round(templateWidth / aspect), 132, 304);
#if WINDOWS
            using var bitmap = GenerateCenteredDefectBitmap(templateWidth, templateHeight, random);
            pool[index] = CreateTemplateFromBitmap(bitmap);
#else
            pool[index] = new DefectTemplate(templateWidth, templateHeight, GenerateCenteredDefectPixels(templateWidth, templateHeight, random));
#endif
        }

        return pool;
    }

    private static byte[] GenerateCenteredDefectPixels(int width, int height, Random random)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var pixels = new byte[checked(width * height)];
        var blobCount = random.Next(2, 5);
        var baseCenterX = (width - 1) / 2.0;
        var baseCenterY = (height - 1) / 2.0;
        var maxJitterX = width * 0.12;
        var maxJitterY = height * 0.12;
        var majorRadius = Math.Min(width, height) * 0.44;

        for (var blobIndex = 0; blobIndex < blobCount; blobIndex++)
        {
            var centerX = baseCenterX + ((random.NextDouble() * 2 - 1) * maxJitterX);
            var centerY = baseCenterY + ((random.NextDouble() * 2 - 1) * maxJitterY);
            var radiusX = majorRadius * (0.8 + (random.NextDouble() * 1.4));
            var radiusY = majorRadius * (0.5 + (random.NextDouble() * 1.6));
            var hardCoreRatio = 0.62 + (random.NextDouble() * 0.16);
            var peak = random.Next(184, 246);

            var left = Math.Max(0, (int)Math.Floor(centerX - radiusX));
            var right = Math.Min(width - 1, (int)Math.Ceiling(centerX + radiusX));
            var top = Math.Max(0, (int)Math.Floor(centerY - radiusY));
            var bottom = Math.Min(height - 1, (int)Math.Ceiling(centerY + radiusY));

            for (var y = top; y <= bottom; y++)
            {
                var normalizedY = (y - centerY) / radiusY;
                var normalizedYSquared = normalizedY * normalizedY;
                if (normalizedYSquared > 1)
                {
                    continue;
                }

                for (var x = left; x <= right; x++)
                {
                    var normalizedX = (x - centerX) / radiusX;
                    var normalizedDistance = (normalizedX * normalizedX) + normalizedYSquared;
                    if (normalizedDistance > 1)
                    {
                        continue;
                    }

                    var distance = Math.Sqrt(normalizedDistance);
                    var intensity = distance <= hardCoreRatio
                        ? 1
                        : Math.Pow(1 - ((distance - hardCoreRatio) / (1 - hardCoreRatio)), 0.8);
                    var value = (byte)Math.Clamp((int)Math.Round(peak * intensity), 0, 255);
                    var offset = (y * width) + x;
                    if (value > pixels[offset])
                    {
                        pixels[offset] = value;
                    }
                }
            }
        }

        return pixels;
    }

#if WINDOWS
    private static Bitmap GenerateMonochromeBitmap(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed)
    {
        _ = noise;
        _ = seed;
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(targetValue, targetValue, targetValue));

        return bitmap;
    }

    private static Bitmap GenerateCenteredDefectBitmap(int width, int height, Random random)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var centerX = width / 2f;
        var centerY = height / 2f;
        var shapeCount = random.Next(2, 5);
        for (var i = 0; i < shapeCount; i++)
        {
            var blobWidth = width * (0.28f + ((float)random.NextDouble() * 0.52f));
            var blobHeight = height * (0.22f + ((float)random.NextDouble() * 0.58f));
            var jitterX = (float)((random.NextDouble() * 2 - 1) * width * 0.14);
            var jitterY = (float)((random.NextDouble() * 2 - 1) * height * 0.14);
            var left = centerX - (blobWidth / 2f) + jitterX;
            var top = centerY - (blobHeight / 2f) + jitterY;
            var intensity = random.Next(168, 242);
            using var brush = new SolidBrush(Color.FromArgb(intensity, intensity, intensity));
            graphics.FillEllipse(brush, left, top, blobWidth, blobHeight);
        }

        return bitmap;
    }

    private static unsafe DefectTemplate CreateTemplateFromBitmap(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new byte[checked(width * height)];
        var bounds = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var source = (byte*)data.Scan0;
            for (var y = 0; y < height; y++)
            {
                var row = source + (y * data.Stride);
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    pixels[rowOffset + x] = row[x * 3];
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return new DefectTemplate(width, height, pixels);
    }
#endif
}