using InfiniteCanvas.Core;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace InfiniteCanvas.Rendering;

public static class SampleImageGenerator
{
    public const int DefaultPixelWidth = 8192;
    public const int DefaultPixelHeight = 4096;
    public const int NoiseBlockSize = 512;
    public const int MaxObjectsPerTile = 256;

    private static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];
    private static readonly IReadOnlyDictionary<string, Bgra32Color> ClassificationColors = new Dictionary<string, Bgra32Color>
    {
        ["Scratch"] = new(60, 90, 245, 255),
        ["Inclusion"] = new(70, 205, 255, 255),
        ["Stain"] = new(120, 220, 120, 255),
        ["Edge defect"] = new(90, 160, 255, 255)
    };
    private sealed record DefectTemplate(
        int Width,
        int Height,
        byte[] Pixels
#if WINDOWS
        , Bitmap Bitmap
#endif
    );

    public static IReadOnlyList<SampleImageTile> GenerateSet(
        int imageCount = 64,
        int pixelWidth = DefaultPixelWidth,
        int pixelHeight = DefaultPixelHeight,
        byte targetValue = 128,
        byte noise = 8,
        int objectsPerTile = 16,
        int columns = 2,
        int seed = 1729,
        int? rows = null,
        int defectPoolSize = 64,
        int circleCount = 3)
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

        if (objectsPerTile is < 0 or > MaxObjectsPerTile)
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
        var tileCount = checked(columns * rowCount);
        if (rows.HasValue && imageCount != tileCount)
        {
            throw new ArgumentException(
                "imageCount must equal columns multiplied by rows when rows is specified.",
                nameof(imageCount));
        }
        var poolSeed = unchecked(seed + 48611);
        var poolRandom = new DeterministicRandom(poolSeed);
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
                new DeterministicRandom(annotationSeed),
                defectTemplatePool);
#if WINDOWS
            // Synthetic backgrounds are Gray8 by definition; avoid a GDI+ 24bpp image and full RGB-to-gray copy.
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromeTiledPixels(pixelWidth, pixelHeight, targetValue, noise, pixelSeed, circleCount),
                annotations,
                targetValue,
                mipLevel => GenerateMonochromeMipPixels(pixelWidth, pixelHeight, targetValue, noise, mipLevel, pixelSeed, circleCount));
#else
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromePixels(pixelWidth, pixelHeight, targetValue, noise, pixelSeed, circleCount),
                annotations,
                targetValue,
                mipLevel => GenerateMonochromeMipPixels(pixelWidth, pixelHeight, targetValue, noise, mipLevel, pixelSeed, circleCount));
#endif
        }

        return tiles;
    }

    public static byte[] GenerateMonochromePixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed = 1729,
        int circleCount = 3)
    {
        return GenerateMonochromePixels(
            width,
            height,
            targetValue,
            noise,
            new DeterministicRandom(seed),
            new DeterministicRandom(unchecked(seed + 0x2F6E2B1)),
            circleCount);
    }

    public static byte[] GenerateMonochromeMipPixels(
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        byte noise,
        int mipLevel,
        int seed = 1729,
        int circleCount = 3)
    {
        if (nativeWidth <= 0 || nativeHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeWidth));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        var (width, height) = BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel);
        var pixels = new byte[checked(width * height)];
        if (noise == 0 && circleCount <= 0)
        {
            Array.Fill(pixels, targetValue);
            return pixels;
        }

        var noiseSpread = Math.Clamp(noise + 8, 8, 24);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (mipLevel == 0)
                {
                    var sampleSeed = unchecked(seed + (x * 73856093) + (y * 19349663));
                    var jitter = noise == 0
                        ? 0
                        : new DeterministicRandom(sampleSeed).Next(-noiseSpread, noiseSpread + 1);
                    pixels[(y * width) + x] = (byte)Math.Clamp(targetValue + jitter, 0, 255);
                    continue;
                }

                var childX = x * 2;
                var childY = y * 2;
                var sum = 0;
                var sampleCount = 0;
                for (var offsetY = 0; offsetY < 2; offsetY++)
                {
                    for (var offsetX = 0; offsetX < 2; offsetX++)
                    {
                        var sampleX = childX + offsetX;
                        var sampleY = childY + offsetY;
                        if (sampleX >= BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel - 1).Width
                            || sampleY >= BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel - 1).Height)
                        {
                            continue;
                        }

                        var sampleSeed = unchecked(seed
                            + (sampleX * 73856093)
                            + (sampleY * 19349663)
                            + (mipLevel * 83492791));
                        var jitter = noise == 0
                            ? 0
                            : new DeterministicRandom(sampleSeed).Next(-noiseSpread, noiseSpread + 1);
                        sum += Math.Clamp(targetValue + jitter, 0, 255);
                        sampleCount++;
                    }
                }

                pixels[(y * width) + x] = (byte)((sum + (sampleCount / 2)) / sampleCount);
            }
        }

        ApplyMipCircles(pixels, width, height, targetValue, circleCount, seed);
        return pixels;
    }

    private static void ApplyMipCircles(
        byte[] pixels,
        int width,
        int height,
        byte targetValue,
        int circleCount,
        int seed)
    {
        var random = new DeterministicRandom(unchecked(seed + 0x2F6E2B1));
        var effectiveCircleCount = Math.Clamp(circleCount, 0, 8);
        var maxRadius = Math.Max(1, Math.Min(width, height) / 10);
        for (var circleIndex = 0; circleIndex < effectiveCircleCount; circleIndex++)
        {
            var centerX = random.Next(0, width);
            var centerY = random.Next(0, height);
            var radius = random.Next(1, maxRadius + 1);
            var circleValue = (byte)Math.Clamp(targetValue - random.Next(10, 34), 0, 255);
            var radiusSquared = radius * radius;
            var left = Math.Max(0, centerX - radius);
            var right = Math.Min(width, centerX + radius + 1);
            var top = Math.Max(0, centerY - radius);
            var bottom = Math.Min(height, centerY + radius + 1);

            for (var y = top; y < bottom; y++)
            {
                var dy = y - centerY;
                var dySquared = dy * dy;
                for (var x = left; x < right; x++)
                {
                    var dx = x - centerX;
                    if ((dx * dx) + dySquared <= radiusSquared)
                    {
                        var offset = (y * width) + x;
                        pixels[offset] = Math.Min(pixels[offset], circleValue);
                    }
                }
            }
        }
    }

    public static byte[] ReduceGray8Box(
        ReadOnlySpan<byte> source,
        (int Width, int Height) sourceDimensions,
        (int Width, int Height) destinationDimensions)
    {
        if (source.Length != checked(sourceDimensions.Width * sourceDimensions.Height))
        {
            throw new ArgumentException("Source length does not match its dimensions.", nameof(source));
        }

        var destination = new byte[checked(destinationDimensions.Width * destinationDimensions.Height)];
        for (var y = 0; y < destinationDimensions.Height; y++)
        {
            var sourceTop = y * 2;
            for (var x = 0; x < destinationDimensions.Width; x++)
            {
                var sourceLeft = x * 2;
                var sum = 0;
                var count = 0;
                for (var sourceY = sourceTop; sourceY < Math.Min(sourceTop + 2, sourceDimensions.Height); sourceY++)
                {
                    for (var sourceX = sourceLeft; sourceX < Math.Min(sourceLeft + 2, sourceDimensions.Width); sourceX++)
                    {
                        sum += source[(sourceY * sourceDimensions.Width) + sourceX];
                        count++;
                    }
                }

                destination[(y * destinationDimensions.Width) + x] = (byte)((sum + (count / 2)) / count);
            }
        }

        return destination;
    }

    private static byte[] GenerateMonochromePixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        DeterministicRandom random,
        DeterministicRandom circleRandom,
        int circleCount)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var pixels = new byte[checked(width * height)];
        var noiseSpread = Math.Clamp(noise + 8, 8, 24);
        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
        {
            var jitter = noise == 0 ? 0 : random.Next(-noiseSpread, noiseSpread + 1);
            pixels[pixelIndex] = (byte)Math.Clamp(targetValue + jitter, 0, 255);
        }

        var effectiveCircleCount = Math.Clamp(circleCount, 0, 8);
        var maxRadius = Math.Max(8, Math.Min(width, height) / 10);
        for (var circleIndex = 0; circleIndex < effectiveCircleCount; circleIndex++)
        {
            var centerX = circleRandom.Next(0, width);
            var centerY = circleRandom.Next(0, height);
            var radius = circleRandom.Next(6, maxRadius + 1);
            var circleValue = (byte)Math.Clamp(targetValue - circleRandom.Next(10, 34), 0, 255);
            var radiusSquared = radius * radius;

            for (var y = Math.Max(0, centerY - radius); y < Math.Min(height, centerY + radius + 1); y++)
            {
                var dy = y - centerY;
                var dySquared = dy * dy;
                for (var x = Math.Max(0, centerX - radius); x < Math.Min(width, centerX + radius + 1); x++)
                {
                    var dx = x - centerX;
                    if ((dx * dx) + dySquared > radiusSquared)
                    {
                        continue;
                    }

                    var offset = (y * width) + x;
                    pixels[offset] = (byte)Math.Min(pixels[offset], circleValue);
                }
            }
        }

        return pixels;
    }

    private static IReadOnlyList<SampleAnnotation> GenerateAnnotations(
        string tileId,
        SpatialBounds tileBounds,
        int count,
        DeterministicRandom random,
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
                defectTemplate.Pixels)
#if WINDOWS
            {
                DefectBitmap = defectTemplate.Bitmap
            };
#else
            ;
#endif
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
        DeterministicRandom random)
    {
        var pool = new DefectTemplate[count];
        for (var index = 0; index < count; index++)
        {
            var aspect = 0.45 + (random.NextDouble() * 1.95);
            var templateWidth = random.Next(156, 276);
            var templateHeight = Math.Clamp((int)Math.Round(templateWidth / aspect), 132, 304);
#if WINDOWS
            var bitmap = GenerateCenteredDefectBitmap(templateWidth, templateHeight, random);
            pool[index] = CreateTemplateFromBitmap(bitmap);
#else
            pool[index] = new DefectTemplate(templateWidth, templateHeight, GenerateCenteredDefectPixels(templateWidth, templateHeight, random));
#endif
        }

        return pool;
    }

    private static byte[] GenerateCenteredDefectPixels(int width, int height, DeterministicRandom random)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var pixels = new byte[checked(width * height)];
        Array.Fill(pixels, (byte)150);
        var blobCount = random.Next(5, 11);
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
            var peak = random.Next(24, 121);

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
                    if (value < pixels[offset])
                    {
                        pixels[offset] = value;
                    }
                }
            }
        }

        return pixels;
    }

#if WINDOWS
    private static byte[] GenerateMonochromeTiledPixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed,
        int circleCount)
    {
        var pixels = new byte[checked(width * height)];
        if (noise == 0 && circleCount <= 0)
        {
            Array.Fill(pixels, targetValue);
            return pixels;
        }

        var noiseBlock = GenerateMonochromePixels(
            NoiseBlockSize,
            NoiseBlockSize,
            targetValue,
            noise,
            new DeterministicRandom(seed),
            new DeterministicRandom(unchecked(seed + 0x2F6E2B1)),
            circleCount: 0);
        for (var y = 0; y < height; y++)
        {
            var sourceRow = (y % NoiseBlockSize) * NoiseBlockSize;
            var destinationOffset = y * width;
            for (var x = 0; x < width; x += NoiseBlockSize)
            {
                var copyLength = Math.Min(NoiseBlockSize, width - x);
                noiseBlock.AsSpan(sourceRow, copyLength).CopyTo(pixels.AsSpan(destinationOffset + x, copyLength));
            }
        }

        ApplyBackgroundCircles(pixels, width, height, targetValue, circleCount, seed);
        return pixels;
    }

    private static void ApplyBackgroundCircles(
        byte[] pixels,
        int width,
        int height,
        byte targetValue,
        int circleCount,
        int seed)
    {
        var random = new DeterministicRandom(unchecked(seed + 0x2F6E2B1));
        var effectiveCircleCount = Math.Clamp(circleCount, 0, 8);
        var maxRadius = Math.Max(8, Math.Min(width, height) / 10);
        for (var circleIndex = 0; circleIndex < effectiveCircleCount; circleIndex++)
        {
            var centerX = random.Next(0, width);
            var centerY = random.Next(0, height);
            var radius = random.Next(6, maxRadius + 1);
            var circleValue = (byte)Math.Clamp(targetValue - random.Next(10, 34), 0, 255);
            var radiusSquared = radius * radius;
            var left = Math.Max(0, centerX - radius);
            var right = Math.Min(width, centerX + radius + 1);
            var top = Math.Max(0, centerY - radius);
            var bottom = Math.Min(height, centerY + radius + 1);

            for (var y = top; y < bottom; y++)
            {
                var dy = y - centerY;
                var dySquared = dy * dy;
                for (var x = left; x < right; x++)
                {
                    var dx = x - centerX;
                    if ((dx * dx) + dySquared <= radiusSquared)
                    {
                        var offset = (y * width) + x;
                        pixels[offset] = Math.Min(pixels[offset], circleValue);
                    }
                }
            }
        }
    }

    private static Bitmap GenerateCenteredDefectBitmap(int width, int height, DeterministicRandom random)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(150, 150, 150));
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var centerX = width / 2f;
        var centerY = height / 2f;
        var shapeCount = random.Next(5, 11);
        for (var i = 0; i < shapeCount; i++)
        {
            var blobWidth = width * (0.28f + ((float)random.NextDouble() * 0.52f));
            var blobHeight = height * (0.22f + ((float)random.NextDouble() * 0.58f));
            var jitterX = (float)((random.NextDouble() * 2 - 1) * width * 0.14);
            var jitterY = (float)((random.NextDouble() * 2 - 1) * height * 0.14);
            var left = centerX - (blobWidth / 2f) + jitterX;
            var top = centerY - (blobHeight / 2f) + jitterY;
            var intensity = random.Next(24, 121);
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

        return new DefectTemplate(width, height, pixels, bitmap);
    }
#endif

    private struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(int seed)
        {
            _state = unchecked((uint)seed) + 0x9E3779B97F4A7C15UL;
        }

        public int Next(int maxValue) => Next(0, maxValue);

        public int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }

            return minValue + (int)(NextUInt64() % (uint)(maxValue - minValue));
        }

        public long NextInt64(long minValue, long maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }

            return minValue + (long)(NextUInt64() % (ulong)(maxValue - minValue));
        }

        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        private ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}