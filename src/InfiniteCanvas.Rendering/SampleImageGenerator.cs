using System.Buffers;
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
    public const int MaxObjectsPerTile = CanvasUserSettings.MaxObjectsPerTile;

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

    public static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];
    public static readonly IReadOnlyDictionary<string, Bgra32Color> ClassificationColors = new Dictionary<string, Bgra32Color>
    {
        ["Scratch"] = new(60, 90, 245, 255),
        ["Inclusion"] = new(70, 205, 255, 255),
        ["Stain"] = new(120, 220, 120, 255),
        ["Edge defect"] = new(90, 160, 255, 255)
    };
        public sealed record DefectTemplate(
        int Width,
        int Height,
        byte[] Pixels
    #if WINDOWS
        , Bitmap Bitmap
    #endif
        ) : IDisposable
        {
        public void Dispose()
        {
    #if WINDOWS
            Bitmap?.Dispose();
    #endif
        }
        }

    // Centralized byte conversion helpers: keep intermediate math in wider types
    // and convert to `byte` only at the pixel-sink boundary.
    private static byte ToByteClamped(int v) => (byte)Math.Clamp(v, 0, 255);
    private static byte ToByteClamped(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
    private const byte DefectBaseValue = 150;

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
        // Preserve original parameter validation so thrown exceptions keep caller-visible parameter names.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        if (!CanvasUserSettings.ValidateObjectsPerTile(objectsPerTile))
        {
            throw new ArgumentOutOfRangeException(nameof(objectsPerTile));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defectPoolSize);

        if (rows is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        // Forwarding overload preserved for backward compatibility.
        var options = new GeneratorOptions(
            ImageCount: imageCount,
            PixelWidth: pixelWidth,
            PixelHeight: pixelHeight,
            TargetValue: targetValue,
            Noise: noise,
            NoiseScale: noiseScale,
            NoiseOctaves: noiseOctaves,
            NoiseLacunarity: noiseLacunarity,
            NoiseGain: noiseGain,
            NoiseAmplitude: noiseAmplitude,
            ObjectsPerTile: objectsPerTile,
            Columns: columns,
            Seed: seed,
            Rows: rows,
            DefectPoolSize: defectPoolSize,
            CircleCount: circleCount);

        return GenerateSet(options);
    }

    public static IReadOnlyList<SampleImageTile> GenerateSet(GeneratorOptions options)
    {
        if (options.ImageCount <= 0)
        {
            throw new ArgumentOutOfRangeException("imageCount");
        }

        if (options.PixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException("pixelWidth");
        }

        if (options.PixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException("pixelHeight");
        }

        if (!CanvasUserSettings.ValidateObjectsPerTile(options.ObjectsPerTile))
        {
            throw new ArgumentOutOfRangeException("objectsPerTile");
        }

        if (options.Columns <= 0)
        {
            throw new ArgumentOutOfRangeException("columns");
        }

        if (options.DefectPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException("defectPoolSize");
        }

        if (options.Rows is <= 0)
        {
            throw new ArgumentOutOfRangeException("rows");
        }

        var rowCount = options.Rows ?? Math.Max(1, (int)Math.Ceiling(options.ImageCount / (double)options.Columns));
        var tileCount = checked(options.Columns * rowCount);
        if (options.Rows.HasValue && options.ImageCount != tileCount)
        {
            throw new ArgumentException(
                "imageCount must equal columns multiplied by rows when rows is specified.",
                "imageCount");
        }

        var poolSeed = unchecked(options.Seed + 48611);
        var poolRandom = new DeterministicRandom(poolSeed);
        var defectTemplatePool = DefectTemplateFactory.Build(options.DefectPoolSize, poolRandom);

        var tiles = new SampleImageTile[tileCount];

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var tileId = $"TILE-{tileIndex + 1:D2}";
            var tileX = (tileIndex % options.Columns) * (double)options.PixelWidth;
            var tileY = (tileIndex / options.Columns) * (double)options.PixelHeight;
            var bounds = new SpatialBounds(tileX, tileY, options.PixelWidth, options.PixelHeight);
            var pixelSeed = options.Seed + 3 * tileIndex;
            var annotationSeed = unchecked(options.Seed + (tileIndex * 130363) + 7919);
            var noiseSettings = new NoiseSettings { Scale = options.NoiseScale, Octaves = options.NoiseOctaves, Lacunarity = options.NoiseLacunarity, Gain = options.NoiseGain, Amplitude = options.NoiseAmplitude };
            var annotations = AnnotationGenerator.GenerateAnnotations(
                tileId,
                bounds,
                options.ObjectsPerTile,
                new DeterministicRandom(annotationSeed),
                defectTemplatePool);
            // Synthetic backgrounds are Gray8 by definition; only the circle mask uses GDI+.
            tiles[tileIndex] = new SampleImageTile(
                tileId,
                bounds,
                options.PixelWidth,
                options.PixelHeight,
                () => GenerateMonochromeMipPixelsSeeded(options.PixelWidth, options.PixelHeight, options.TargetValue, options.Noise, 0, pixelSeed, options.CircleCount, noiseSettings, (float)bounds.X, (float)bounds.Y, tileId),
                annotations,
                options.TargetValue,
                mipLevel => GenerateMonochromeMipPixelsSeeded(options.PixelWidth, options.PixelHeight, options.TargetValue, options.Noise, mipLevel, pixelSeed, options.CircleCount, noiseSettings, (float)bounds.X, (float)bounds.Y, tileId),
                cancellablePixelFactory: token => GenerateMonochromeMipPixelsSeeded(options.PixelWidth, options.PixelHeight, options.TargetValue, options.Noise, 0, pixelSeed, options.CircleCount, noiseSettings, (float)bounds.X, (float)bounds.Y, tileId, token),
                cancellableMipPixelFactory: (mipLevel, token) => GenerateMonochromeMipPixelsSeeded(options.PixelWidth, options.PixelHeight, options.TargetValue, options.Noise, mipLevel, pixelSeed, options.CircleCount, noiseSettings, (float)bounds.X, (float)bounds.Y, tileId, token));
            // Attach a reference to the shared defect template pool so eviction paths
            // can dispose platform bitmaps when the tile's images are evicted/regenerated.
            tiles[tileIndex].DefectTemplatePool = defectTemplatePool;
        }

        return tiles;
    }

    // Legacy full-resolution entry removed. Use the mip-aware API instead.

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
        float worldOriginY = 0f,
        string? tileLabel = null,
        CancellationToken cancellationToken = default)
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
            worldOriginY,
            tileLabel,
            cancellationToken);
    }

    // Overload that defaults to mip level 0 for callers that omit the mip.
    public static byte[] GenerateMonochromeMipPixelsSeeded(
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        byte noise,
        int seed = 1729,
        int circleCount = 3,
        NoiseSettings? noiseSettings = null,
        float worldOriginX = 0f,
        float worldOriginY = 0f,
        string? tileLabel = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateMonochromeMipPixelsSeeded(nativeWidth, nativeHeight, targetValue, noise, 0, seed, circleCount, noiseSettings, worldOriginX, worldOriginY, tileLabel, cancellationToken);
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
        float worldOriginY = 0f,
        string? tileLabel = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nativeWidth <= 0 || nativeHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeWidth));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        var (width, height) = BackgroundTileMipPolicy.GetDimensions(nativeWidth, nativeHeight, mipLevel);
        var pixels = new byte[checked(width * height)];
        if (noise == 0)
        {
            Array.Fill(pixels, targetValue);
        }
        else
        {
            var mipScale = 1 << mipLevel;
            var stepSize = mipScale * (float)(noiseSettings?.Scale ?? NoiseSettings.Default.Scale);
            GenerateNoisePixelsCore(pixels, width, height, targetValue, noise, seed, noiseSettings ?? NoiseSettings.Default, worldOriginX, worldOriginY, stepSize, cancellationToken, mipLevel);
        }

        if (circleCount > 0 || !string.IsNullOrWhiteSpace(tileLabel))
        {
            using var measurement = RenderingDiagnostics.MeasureCurrent(RenderingStage.CircleRasterization, mipLevel);
            ApplyMipDetails(pixels, width, height, nativeWidth, nativeHeight, targetValue, circleCount, seed, tileLabel, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RenderingDiagnostics.RecordCurrent(
            RenderingDiagnosticOutcome.Generated,
            mipLevel,
            sampleCount: pixels.LongLength,
            residentPayloadBytes: pixels.LongLength);
        return pixels;
    }

    private static void ApplyMipDetails(
        byte[] pixels,
        int width,
        int height,
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        int circleCount,
        int seed,
        string? tileLabel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var random = new DeterministicRandom(unchecked(seed + 0x2F6E2B1));
        var effectiveCircleCount = Math.Clamp(circleCount, 0, 8);
        var maxRadius = Math.Max(8, Math.Min(nativeWidth, nativeHeight) / 10);
        var scaleX = width / (double)nativeWidth;
        var scaleY = height / (double)nativeHeight;
        var circles = new (float CenterX, float CenterY, float Radius, byte Value)[effectiveCircleCount];
            for (var circleIndex = 0; circleIndex < effectiveCircleCount; circleIndex++)
        {
                cancellationToken.ThrowIfCancellationRequested();
            var centerX = random.Next(0, nativeWidth);
            var centerY = random.Next(0, nativeHeight);
            var radius = random.Next(6, maxRadius + 1);
            var circleValue = ToByteClamped(targetValue - random.Next(10, 34));
            circles[circleIndex] = ((float)(centerX * scaleX), (float)(centerY * scaleY), (float)(radius * Math.Min(scaleX, scaleY)), circleValue);
        }

#if WINDOWS
        ApplyDetailsWithGdiPlus(pixels, width, height, circles, tileLabel, cancellationToken);
#else
        ApplyCirclesWithRasterizer(pixels, width, height, circles, cancellationToken);
#endif
    }

#if WINDOWS
    private static readonly SemaphoreSlim GdiPlusGate = new(1, 1);

    private static void ApplyDetailsWithGdiPlus(
        byte[] pixels,
        int width,
        int height,
        ReadOnlySpan<(float CenterX, float CenterY, float Radius, byte Value)> circles,
        string? tileLabel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GdiPlusGate.Wait(cancellationToken);
        try
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                cancellationToken.ThrowIfCancellationRequested();
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                foreach (var circle in circles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var brush = new SolidBrush(Color.FromArgb(255, circle.Value, circle.Value, circle.Value));
                    graphics.FillEllipse(
                        brush,
                        circle.CenterX - circle.Radius,
                        circle.CenterY - circle.Radius,
                        circle.Radius * 2,
                        circle.Radius * 2);
                }

                if (!string.IsNullOrWhiteSpace(tileLabel))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var font = new Font(
                        FontFamily.GenericSansSerif,
                        Math.Max(8f, height / 12f),
                        FontStyle.Regular,
                        GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Color.FromArgb(255, 16, 16, 16));
                    graphics.DrawString(tileLabel, font, brush, 0f, 0f);
                }
            }

            var bounds = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                unsafe
                {
                    var source = (byte*)data.Scan0;
                    for (var y = 0; y < height; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
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
        finally
        {
            GdiPlusGate.Release();
        }
    }
#endif

    private static void ApplyCirclesWithRasterizer(
        byte[] pixels,
        int width,
        int height,
        ReadOnlySpan<(float CenterX, float CenterY, float Radius, byte Value)> circles,
        CancellationToken cancellationToken)
    {
        foreach (var circle in circles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var radiusSquared = circle.Radius * circle.Radius;
            var left = Math.Max(0, (int)Math.Floor(circle.CenterX - circle.Radius));
            var right = Math.Min(width, (int)Math.Ceiling(circle.CenterX + circle.Radius) + 1);
            var top = Math.Max(0, (int)Math.Floor(circle.CenterY - circle.Radius));
            var bottom = Math.Min(height, (int)Math.Ceiling(circle.CenterY + circle.Radius) + 1);
            for (var y = top; y < bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                destination[(y * destinationDimensions.Width) + x] = ToByteClamped((sum + (count / 2)) / count);
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
        // Legacy full-resolution convenience removed in favor of mip-aware API.
        return GenerateMonochromeMipPixels(
            width,
            height,
            targetValue,
            noise,
            0,
            seed,
            circleCount,
            noiseSettings,
            worldOriginX,
            worldOriginY);
    }

    // Overload that defaults to mip level 0 for callers that omit the mip.
    public static byte[] GenerateMonochromeMipPixels(
        int nativeWidth,
        int nativeHeight,
        byte targetValue,
        byte noise,
        int seed,
        int circleCount,
        NoiseSettings noiseSettings,
        float worldOriginX = 0f,
        float worldOriginY = 0f)
    {
        return GenerateMonochromeMipPixels(nativeWidth, nativeHeight, targetValue, noise, 0, seed, circleCount, noiseSettings, worldOriginX, worldOriginY);
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
        float stepSize,
        CancellationToken cancellationToken,
        int mipLevel)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (noise == 0)
        {
            Array.Fill(pixels, targetValue);
            return;
        }

        var noiseSpread = Math.Clamp(noise + 8, 8, 24);
        var pixelCount = checked(width * height);
        var noiseBuffer = ArrayPool<float>.Shared.Rent(pixelCount);
        try
        {
            using var fastNoise = CreateFastNoise(noiseSettings);
            cancellationToken.ThrowIfCancellationRequested();
            float noiseMin;
            float noiseMax;
            using (RenderingDiagnostics.MeasureCurrent(RenderingStage.NativeNoiseGeneration, mipLevel))
            {
                var outputMinMax = fastNoise.GenUniformGrid2D(
                    noiseBuffer.AsSpan(0, pixelCount),
                    worldOriginX,
                    worldOriginY,
                    width,
                    height,
                    stepSize,
                    stepSize,
                    seed);
                noiseMin = outputMinMax.min;
                noiseMax = outputMinMax.max;
            }

            var range = noiseMax - noiseMin;
            if (range <= 0.0f)
            {
                Array.Fill(pixels, targetValue);
                return;
            }

            var jitterScale = noiseSpread * (float)Math.Max(0.0, noiseSettings.Amplitude);
            var noiseToJitterScale = (2.0f * jitterScale) / range;
            var noiseToJitterOffset = (-noiseMin * noiseToJitterScale) - jitterScale;
            using (RenderingDiagnostics.MeasureCurrent(RenderingStage.Gray8Normalization, mipLevel))
            {
                for (var index = 0; index < pixelCount; index++)
                {
                    if ((index & 0x3FFF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    var scaledJitter = (noiseBuffer[index] * noiseToJitterScale) + noiseToJitterOffset;
                    var jitter = scaledJitter >= 0.0f
                        ? (int)(scaledJitter + 0.5f)
                        : (int)(scaledJitter - 0.5f);
                    pixels[index] = ToByteClamped(targetValue + jitter);
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(noiseBuffer);
        }
    }

    private static FastNoise CreateFastNoise(NoiseSettings noiseSettings)
    {
        var fastNoise = new FastNoise("FractalFBm");
        fastNoise.Set("Source", new FastNoise("Simplex"));
        fastNoise.Set("Octaves", Math.Max(1, noiseSettings.Octaves));
        fastNoise.Set("Gain", (float)Math.Clamp(noiseSettings.Gain, 0.0, 1.0));
        fastNoise.Set("Lacunarity", (float)Math.Clamp(noiseSettings.Lacunarity, 0.0, 16.0));
        return fastNoise;
    }

    public static (double Min, double Max) GetClassAspectRange(string classification)
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

    // Defect template pool creation moved to DefectTemplateFactory.

    public static byte[] GenerateCenteredDefectPixels(int width, int height, DeterministicRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var pixels = new byte[checked(width * height)];
        Array.Fill(pixels, DefectBaseValue);
        var circleCount = random.Next(2, 6);
        var baseCenterX = (width - 1) / 2.0;
        var baseCenterY = (height - 1) / 2.0;
        var maxJitterX = width * 0.24;
        var maxJitterY = height * 0.24;
        var minimumRadius = Math.Max(4.0, Math.Min(width, height) * 0.07);
        var maximumRadius = Math.Max(minimumRadius, Math.Min(width, height) * 0.16);

        for (var circleIndex = 0; circleIndex < circleCount; circleIndex++)
        {
            var centerX = baseCenterX + ((random.NextDouble() * 2 - 1) * maxJitterX);
            var centerY = baseCenterY + ((random.NextDouble() * 2 - 1) * maxJitterY);
            var radius = minimumRadius + (random.NextDouble() * (maximumRadius - minimumRadius));
            var value = ToByteClamped(random.Next(24, 236));

            var left = Math.Max(0, (int)Math.Floor(centerX - radius));
            var right = Math.Min(width - 1, (int)Math.Ceiling(centerX + radius));
            var top = Math.Max(0, (int)Math.Floor(centerY - radius));
            var bottom = Math.Min(height - 1, (int)Math.Ceiling(centerY + radius));

            for (var y = top; y <= bottom; y++)
            {
                var dy = y - centerY;

                for (var x = left; x <= right; x++)
                {
                    var dx = x - centerX;
                    if ((dx * dx) + (dy * dy) > radius * radius)
                    {
                        continue;
                    }

                    var offset = (y * width) + x;
                    pixels[offset] = value;
                }
            }
        }

        return pixels;
    }

    // Bitmap creation helper moved to DefectTemplateFactory for platform isolation.

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    public struct DeterministicRandom(int seed)
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