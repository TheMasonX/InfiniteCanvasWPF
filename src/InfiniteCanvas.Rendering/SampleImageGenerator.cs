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

    public sealed class NoiseSettings
    {
        public double Scale { get; set; }
        public int Octaves { get; set; }
        public double Lacunarity { get; set; }
        public double Gain { get; set; }
        public double Amplitude { get; set; }

        public NoiseSettings() { }

        public NoiseSettings(double scale, int octaves, double lacunarity, double gain, double amplitude)
        {
            Scale = scale;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Gain = gain;
            Amplitude = amplitude;
        }

        public static NoiseSettings Default => new NoiseSettings(scale: 1, octaves: 5, lacunarity: 2.5, gain: 0.6, amplitude: 1.0);
    }

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
        double noiseScale = 1.0,
        int noiseOctaves = 5,
        double noiseLacunarity = 2.5,
        double noiseGain = 0.6,
        double noiseAmplitude = 1.0,
        int objectsPerTile = 16,
        int columns = 2,
        int seed = 1729,
        int? rows = null,
        int defectPoolSize = 64,
        int circleCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageCount);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        if (objectsPerTile is < 0 or > MaxObjectsPerTile)
        {
            throw new ArgumentOutOfRangeException(nameof(objectsPerTile));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defectPoolSize);

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
            var pixelSeed = seed + 3 * tileIndex;
            var annotationSeed = unchecked(seed + (tileIndex * 130363) + 7919);
            var noiseSettings = new NoiseSettings { Scale = noiseScale, Octaves = noiseOctaves, Lacunarity = noiseLacunarity, Gain = noiseGain, Amplitude = noiseAmplitude };
            var annotations = GenerateAnnotations(
                tileId,
                bounds,
                objectsPerTile,
                new DeterministicRandom(annotationSeed),
                defectTemplatePool);
            // Synthetic backgrounds are Gray8 by definition; only the circle mask uses GDI+.
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                pixelWidth,
                pixelHeight,
                () => GenerateMonochromeMipPixelsSeeded(pixelWidth, pixelHeight, targetValue, noise, 0, pixelSeed, circleCount, noiseSettings, (float)bounds.X, (float)bounds.Y),
                annotations,
                targetValue,
                mipLevel => GenerateMonochromeMipPixelsSeeded(pixelWidth, pixelHeight, targetValue, noise, mipLevel, pixelSeed, circleCount, noiseSettings, (float)bounds.X, (float)bounds.Y));
        }

        return tiles;
    }

    public static byte[] GenerateMonochromePixelsSeeded(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed = 1729,
        int circleCount = 3,
        NoiseSettings? noiseSettings = null,
        float worldOriginX = 0f,
        float worldOriginY = 0f)
    {
        return GenerateMonochromePixels(
            width,
            height,
            targetValue,
            noise,
            seed,
            circleCount,
            noiseSettings ?? NoiseSettings.Default,
            worldOriginX,
            worldOriginY);
    }

    public static byte[] GenerateMonochromeMipPixelsSeeded(
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        byte noise,
        int mipLevel,
        int seed = 1729,
        int circleCount = 3,
        NoiseSettings? noiseSettings = null,
        float worldOriginX = 0f,
        float worldOriginY = 0f)
    {
        return GenerateMonochromeMipPixels(
            nativeWidth,
            nativeHeight,
            targetValue,
            noise,
            mipLevel,
            seed,
            circleCount,
            noiseSettings,
            worldOriginX,
            worldOriginY);
    }

    public static byte[] GenerateMonochromeMipPixels(
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        byte noise,
        int mipLevel,
        int seed = 1729,
        int circleCount = 3,
        NoiseSettings? noiseSettings = null,
        float worldOriginX = 0f,
        float worldOriginY = 0f)
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

        var stepSize = (float)(Math.Pow(2.0, mipLevel) * (noiseSettings?.Scale ?? NoiseSettings.Default.Scale));
        GenerateNoisePixelsCore(pixels, width, height, targetValue, noise, seed, noiseSettings ?? NoiseSettings.Default, worldOriginX, worldOriginY, stepSize);
        ApplyMipCircles(pixels, width, height, nativeWidth, nativeHeight, targetValue, circleCount, seed);
        return pixels;
    }

    private static void ApplyMipCircles(
        byte[] pixels,
        int width,
        int height,
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        int circleCount,
        int seed)
    {
        var random = new DeterministicRandom(unchecked(seed + 0x2F6E2B1));
        var effectiveCircleCount = Math.Clamp(circleCount, 0, 8);
        var maxRadius = Math.Max(8, Math.Min(nativeWidth, nativeHeight) / 10);
        var scaleX = width / (double)nativeWidth;
        var scaleY = height / (double)nativeHeight;
        var circles = new (float CenterX, float CenterY, float Radius, byte Value)[effectiveCircleCount];
        for (var circleIndex = 0; circleIndex < effectiveCircleCount; circleIndex++)
        {
            var centerX = random.Next(0, nativeWidth);
            var centerY = random.Next(0, nativeHeight);
            var radius = random.Next(6, maxRadius + 1);
            var circleValue = (byte)Math.Clamp(targetValue - random.Next(10, 34), 0, 255);
            circles[circleIndex] = ((float)(centerX * scaleX), (float)(centerY * scaleY), (float)(radius * Math.Min(scaleX, scaleY)), circleValue);
        }

#if WINDOWS
        ApplyCirclesWithGdiPlus(pixels, width, height, circles);
#else
        ApplyCirclesWithRasterizer(pixels, width, height, circles);
#endif
    }

#if WINDOWS
    private static void ApplyCirclesWithGdiPlus(
        byte[] pixels,
        int width,
        int height,
        ReadOnlySpan<(float CenterX, float CenterY, float Radius, byte Value)> circles)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (var circle in circles)
            {
                using var brush = new SolidBrush(Color.FromArgb(255, circle.Value, circle.Value, circle.Value));
                graphics.FillEllipse(
                    brush,
                    circle.CenterX - circle.Radius,
                    circle.CenterY - circle.Radius,
                    circle.Radius * 2,
                    circle.Radius * 2);
            }
        }

        var bounds = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                var source = (byte*)data.Scan0;
                for (var y = 0; y < height; y++)
                {
                    var row = source + (y * data.Stride);
                    for (var x = 0; x < width; x++)
                    {
                        var sourceOffset = x * 4;
                        if (row[sourceOffset + 3] == 0)
                        {
                            continue;
                        }

                        var offset = (y * width) + x;
                        pixels[offset] = Math.Min(pixels[offset], row[sourceOffset + 2]);
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
#endif

    private static void ApplyCirclesWithRasterizer(
        byte[] pixels,
        int width,
        int height,
        ReadOnlySpan<(float CenterX, float CenterY, float Radius, byte Value)> circles)
    {
        foreach (var circle in circles)
        {
            var radiusSquared = circle.Radius * circle.Radius;
            var left = Math.Max(0, (int)Math.Floor(circle.CenterX - circle.Radius));
            var right = Math.Min(width, (int)Math.Ceiling(circle.CenterX + circle.Radius) + 1);
            var top = Math.Max(0, (int)Math.Floor(circle.CenterY - circle.Radius));
            var bottom = Math.Min(height, (int)Math.Ceiling(circle.CenterY + circle.Radius) + 1);
            for (var y = top; y < bottom; y++)
            {
                var dy = y - circle.CenterY;
                for (var x = left; x < right; x++)
                {
                    var dx = x - circle.CenterX;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                    {
                        var offset = (y * width) + x;
                        pixels[offset] = Math.Min(pixels[offset], circle.Value);
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

    public static byte[] GenerateMonochromePixels(
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed,
        int circleCount,
        NoiseSettings noiseSettings,
        float worldOriginX = 0f,
        float worldOriginY = 0f)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var pixels = new byte[checked(width * height)];
        if (noise == 0 && circleCount <= 0)
        {
            Array.Fill(pixels, targetValue);
            return pixels;
        }

        GenerateNoisePixelsCore(pixels, width, height, targetValue, noise, seed, noiseSettings, worldOriginX, worldOriginY, 1.0f);
        ApplyMipCircles(pixels, width, height, width, height, targetValue, circleCount, seed);
        return pixels;
    }

    private static void GenerateNoisePixelsCore(
        byte[] pixels,
        int width,
        int height,
        byte targetValue,
        byte noise,
        int seed,
        NoiseSettings noiseSettings,
        float worldOriginX,
        float worldOriginY,
        float stepSize)
    {
        if (noise == 0)
        {
            Array.Fill(pixels, targetValue);
            return;
        }

        var noiseSpread = Math.Clamp(noise + 8, 8, 24);
        var noiseBuffer = new float[checked(width * height)];
        using var fastNoise = CreateFastNoise(noiseSettings, seed);
        fastNoise.GenUniformGrid2D(noiseBuffer, worldOriginX, worldOriginY, width, height, stepSize, stepSize, seed);

        var noiseMin = float.PositiveInfinity;
        var noiseMax = float.NegativeInfinity;
        for (var index = 0; index < noiseBuffer.Length; index++)
        {
            var value = noiseBuffer[index];
            if (value < noiseMin)
            {
                noiseMin = value;
            }

            if (value > noiseMax)
            {
                noiseMax = value;
            }
        }

        var range = noiseMax - noiseMin;
        if (range <= 0.0f)
        {
            Array.Fill(pixels, targetValue);
            return;
        }

        for (var index = 0; index < noiseBuffer.Length; index++)
        {
            var normalized = (noiseBuffer[index] - noiseMin) / range;
            var jitter = (int)Math.Round(((normalized * 2.0f) - 1.0f) * noiseSpread * Math.Max(0.0, noiseSettings.Amplitude));
            pixels[index] = (byte)Math.Clamp(targetValue + jitter, 0, 255);
        }
    }

    private static FastNoise CreateFastNoise(NoiseSettings noiseSettings, int seed)
    {
        var fastNoise = new FastNoise("FractalFBm");
        fastNoise.Set("Source", new FastNoise("Simplex"));
        fastNoise.Set("Octaves", Math.Max(1, noiseSettings.Octaves));
        fastNoise.Set("Gain", (float)Math.Clamp(noiseSettings.Gain, 0.0, 1.0));
        fastNoise.Set("Lacunarity", (float)Math.Clamp(noiseSettings.Lacunarity, 0.0, 16.0));
        return fastNoise;
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
                defectTemplate.Pixels
#if WINDOWS
            )
            {
                DefectBitmap = defectTemplate.Bitmap
            };
#else
            );
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
            var pixels = GenerateCenteredDefectPixels(templateWidth, templateHeight, random);
#if WINDOWS
            pool[index] = new DefectTemplate(
                templateWidth,
                templateHeight,
                pixels,
                CreateBitmapFromPixels(templateWidth, templateHeight, pixels));
#else
            pool[index] = new DefectTemplate(templateWidth, templateHeight, pixels);
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
    private static unsafe Bitmap CreateBitmapFromPixels(int width, int height, byte[] pixels)
    {
        var bounds = new Rectangle(0, 0, width, height);
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
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
                    row[x * 3] = value;
                    row[(x * 3) + 1] = value;
                    row[(x * 3) + 2] = value;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
    #endif

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private struct DeterministicRandom(int seed)
    {
        private ulong _state = unchecked((uint)seed) + 0x9E3779B97F4A7C15UL;

        public int Next(int maxValue) => Next(0, maxValue);

        public int Next(int minValue, int maxValue)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minValue, maxValue);

            return minValue + (int)(NextUInt64() % (uint)(maxValue - minValue));
        }

        public long NextInt64(long minValue, long maxValue)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minValue, maxValue);

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