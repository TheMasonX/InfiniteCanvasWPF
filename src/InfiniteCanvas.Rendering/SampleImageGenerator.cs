using InfiniteCanvas.Core;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace InfiniteCanvas.Rendering;

public static class SampleImageGenerator
{
    private static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];
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
        if (imageCount <= 0 || pixelWidth <= 0 || pixelHeight <= 0 || objectsPerTile < 0 || columns <= 0 || defectPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageCount));
        }

        if (rows is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        var rowCount = rows ?? Math.Max(1, (int)Math.Ceiling(imageCount / (double)columns));
        var tileCount = rows.HasValue ? checked(columns * rowCount) : imageCount;
        var poolSeed = unchecked(seed + 48611);
        var poolRandom = new Random(poolSeed);
        var defectTemplatePool = BuildDefectTemplatePool(defectPoolSize, 192, 192, poolRandom);

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
                annotations);
#else
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromePixels(pixelWidth, pixelHeight, targetValue, noise, pixelSeed),
                annotations);
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
        Random random,
        IReadOnlyList<DefectTemplate> defectTemplatePool)
    {
        var annotations = new SampleAnnotation[count];

        for (var index = 0; index < count; index++)
        {
            var size = random.Next(70, 201);
            var localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - size));
            var localY = random.Next(0, Math.Max(1, (int)tileBounds.Height - size));
            var objectId = random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12");
            var color = new Bgra32Color(
                (byte)random.Next(12, 56),
                (byte)random.Next(12, 56),
                (byte)random.Next(210, 256),
                byte.MaxValue);
            var defectWidth = checked(size * 2);
            var defectHeight = checked(size * 2);
            var defectTemplate = defectTemplatePool[random.Next(defectTemplatePool.Count)];
            var defectPixels = ResampleTemplate(defectTemplate, defectWidth, defectHeight);

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
                },
                defectWidth,
                defectHeight,
                defectPixels);
        }

        return annotations;
    }

    private static IReadOnlyList<DefectTemplate> BuildDefectTemplatePool(
        int count,
        int width,
        int height,
        Random random)
    {
        var pool = new DefectTemplate[count];
        for (var index = 0; index < count; index++)
        {
#if WINDOWS
            using var bitmap = GenerateCenteredDefectBitmap(width, height, random);
            pool[index] = CreateTemplateFromBitmap(bitmap);
#else
            pool[index] = new DefectTemplate(width, height, GenerateCenteredDefectPixels(width, height, random));
#endif
        }

        return pool;
    }

    private static byte[] ResampleTemplate(DefectTemplate template, int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWidth));
        }

        var output = new byte[checked(targetWidth * targetHeight)];
        var source = template.Pixels;

        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = ((y + 0.5) * template.Height / targetHeight) - 0.5;
            var top = Math.Clamp((int)Math.Floor(sourceY), 0, template.Height - 1);
            var bottom = Math.Clamp(top + 1, 0, template.Height - 1);
            var yLerp = sourceY - top;

            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = ((x + 0.5) * template.Width / targetWidth) - 0.5;
                var left = Math.Clamp((int)Math.Floor(sourceX), 0, template.Width - 1);
                var right = Math.Clamp(left + 1, 0, template.Width - 1);
                var xLerp = sourceX - left;

                var topLeft = source[(top * template.Width) + left];
                var topRight = source[(top * template.Width) + right];
                var bottomLeft = source[(bottom * template.Width) + left];
                var bottomRight = source[(bottom * template.Width) + right];

                var topValue = Lerp(topLeft, topRight, xLerp);
                var bottomValue = Lerp(bottomLeft, bottomRight, xLerp);
                var value = Lerp(topValue, bottomValue, yLerp);
                output[(y * targetWidth) + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }
        }

        return output;
    }

    private static double Lerp(double start, double end, double amount)
    {
        return start + ((end - start) * amount);
    }

    private static byte[] GenerateCenteredDefectPixels(int width, int height, Random random)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var pixels = new byte[checked(width * height)];
        var blobCount = random.Next(1, 4);
        var baseCenterX = (width - 1) / 2.0;
        var baseCenterY = (height - 1) / 2.0;
        var maxJitterX = width * 0.07;
        var maxJitterY = height * 0.07;
        var majorRadius = Math.Min(width, height) * 0.28;

        for (var blobIndex = 0; blobIndex < blobCount; blobIndex++)
        {
            var centerX = baseCenterX + ((random.NextDouble() * 2 - 1) * maxJitterX);
            var centerY = baseCenterY + ((random.NextDouble() * 2 - 1) * maxJitterY);
            var radiusX = majorRadius * (0.85 + (random.NextDouble() * 0.35));
            var radiusY = majorRadius * (0.85 + (random.NextDouble() * 0.35));
            var hardCoreRatio = 0.5 + (random.NextDouble() * 0.15);
            var peak = random.Next(172, 236);

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
                        : Math.Pow(1 - ((distance - hardCoreRatio) / (1 - hardCoreRatio)), 1.2);
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
    private static unsafe Bitmap GenerateMonochromeBitmap(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var random = new Random(seed);
        var minimum = Math.Max(byte.MinValue, targetValue - noise);
        var maximum = Math.Min(byte.MaxValue, targetValue + noise);
        var bounds = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var destination = (byte*)data.Scan0;
            for (var y = 0; y < height; y++)
            {
                var row = destination + (y * data.Stride);
                for (var x = 0; x < width; x++)
                {
                    var value = (byte)random.Next(minimum, maximum + 1);
                    var channelOffset = x * 3;
                    row[channelOffset] = value;
                    row[channelOffset + 1] = value;
                    row[channelOffset + 2] = value;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static unsafe Bitmap GenerateCenteredDefectBitmap(int width, int height, Random random)
    {
        var pixels = GenerateCenteredDefectPixels(width, height, random);
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var bounds = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var destination = (byte*)data.Scan0;
            for (var y = 0; y < height; y++)
            {
                var row = destination + (y * data.Stride);
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    var value = pixels[rowOffset + x];
                    var channelOffset = x * 3;
                    row[channelOffset] = value;
                    row[channelOffset + 1] = value;
                    row[channelOffset + 2] = value;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
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