#if WINDOWS
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InfiniteCanvas.Core;
using Microsoft.Win32.SafeHandles;
using Gdi = System.Drawing;
using GdiImaging = System.Drawing.Imaging;

namespace InfiniteCanvas.Rendering;

/// <summary>
/// Creates <see cref="InteropBitmap"/> instances that map a native memory section
/// directly into WPF bitmaps. The returned <see cref="InteropBitmap"/> values are
/// backed by the factory's file-mapping section and therefore are only valid while
/// the factory (and its underlying file mapping) remains alive.
/// </summary>
/// <remarks>
/// - Callers should keep the <see cref="ZeroCopyBitmapFactory"/> instance alive
///   for the duration of any <see cref="InteropBitmap"/> usage. Disposing the
///   factory invalidates previously returned bitmaps.
/// - The implementation uses <c>PixelFormats.Bgra32</c> and a tightly-packed
///   stride of <c>width * 4</c> bytes; callers should not assume other formats.
/// </remarks>
public sealed class ZeroCopyBitmapFactory : IDisposable
{
    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x0002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    private readonly object _lifetimeGate = new();
    private readonly Bgra32BufferLayout _layout;
    private SafeFileMappingHandle? _section;
    private IntPtr _view;

    public ZeroCopyBitmapFactory(int width, int height)
    {
        _layout = new Bgra32BufferLayout(width, height);
        var byteCount = (ulong)_layout.ByteCount;

        _section = CreateFileMapping(
            InvalidHandleValue,
            IntPtr.Zero,
            PageReadWrite,
            (uint)(byteCount >> 32),
            (uint)byteCount,
            null);

        if (_section.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _view = MapViewOfFile(_section, FileMapWrite, 0, 0, (nuint)_layout.ByteCount);
        if (_view == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _section.Dispose();
            _section = null;
            throw new Win32Exception(error);
        }
    }

    public int Width => _layout.Width;

    public int Height => _layout.Height;

    ~ZeroCopyBitmapFactory()
    {
        Dispose(false);
    }

    public unsafe InteropBitmap GenerateFrozenBitmap(
        IEnumerable<ScreenPoint> screenPoints,
        Bgra32Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(screenPoints);

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_section is null, this);

            NativeMemory.Clear((void*)_view, (nuint)_layout.ByteCount);

            var pixels = (byte*)_view;
            var pixelColor = color ?? Bgra32Color.OpaqueBlue;

            foreach (var point in screenPoints)
            {
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                {
                    continue;
                }

                var x = (int)point.X;
                var y = (int)point.Y;
                if (!_layout.Contains(x, y))
                {
                    continue;
                }

                var offset = _layout.GetPixelOffset(x, y);
                pixels[offset] = pixelColor.Blue;
                pixels[offset + 1] = pixelColor.Green;
                pixels[offset + 2] = pixelColor.Red;
                pixels[offset + 3] = pixelColor.Alpha;
            }

            // The returned InteropBitmap references the factory's memory section.
            // Keep the factory instance alive while consumers hold references to the bitmap.
            var bitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(
                _section.DangerousGetHandle(),
                _layout.Width,
                _layout.Height,
                PixelFormats.Bgra32,
                _layout.Stride,
                0);

            bitmap.Freeze();
            return bitmap;
        }
    }

    public unsafe InteropBitmap GenerateFrozenBitmap(
        IReadOnlyList<SampleImageTile> tiles,
        IReadOnlyList<SampleAnnotation> annotations,
        CameraSnapshot camera,
        Func<SampleImageTile, BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry = null,
        double minimumSparseTilePixelSize = 0,
        bool showBackgroundImages = true,
        bool showSparseImageTiles = true)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(annotations);

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_section is null, this);
            NativeMemory.Clear((void*)_view, (nuint)_layout.ByteCount);

            var pixels = (byte*)_view;
            if (showBackgroundImages)
            {
                foreach (var tile in tiles)
                {
                    DrawTile(pixels, tile, camera, tryReserveCacheEntry, minimumSparseTilePixelSize);
                }
            }

            if (showSparseImageTiles)
            {
                foreach (var annotation in annotations)
                {
                    DrawDefectPatch(pixels, annotation, camera);
                }
            }

            // The returned InteropBitmap references the factory's memory section.
            // Keep the factory instance alive while consumers hold references to the bitmap.
            var bitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(
                _section.DangerousGetHandle(),
                _layout.Width,
                _layout.Height,
                PixelFormats.Bgra32,
                _layout.Stride,
                0);

            bitmap.Freeze();
            return bitmap;
        }
    }

    private unsafe void DrawTile(
        byte* destination,
        SampleImageTile tile,
        CameraSnapshot camera,
        Func<SampleImageTile, BackgroundTileCacheKey, long, ICacheReservation?>? tryReserveCacheEntry,
        double minimumSparseTilePixelSize)
    {
        var topLeft = camera.WorldToScreen(tile.Bounds.X, tile.Bounds.Y);
        var bottomRight = camera.WorldToScreen(tile.Bounds.Right, tile.Bounds.Bottom);
        var left = Math.Clamp((int)Math.Floor(topLeft.X), 0, _layout.Width);
        var top = Math.Clamp((int)Math.Floor(topLeft.Y), 0, _layout.Height);
        var right = Math.Clamp((int)Math.Ceiling(bottomRight.X), 0, _layout.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(bottomRight.Y), 0, _layout.Height);
        if (left >= right || top >= bottom)
        {
            return;
        }

        var mipLevel = BackgroundTileMipPolicy.SelectMipLevel(camera);
        byte[]? sourcePixels = null;
        var hasSourcePixels = tile.TryGetPixelsNonBlocking(
            mipLevel,
            out sourcePixels,
            out var residentMipLevel,
            tryReserveCacheEntry is null ? null : (key, byteCost) => tryReserveCacheEntry(tile, key, byteCost));
        var sourceDimensions = BackgroundTileMipPolicy.GetDimensions(tile.PixelWidth, tile.PixelHeight, residentMipLevel);
        var placeholder = tile.PlaceholderValue;
        var cameraOffsetX = camera.OffsetX;
        var cameraOffsetY = camera.OffsetY;
        var cameraScaleX = camera.ScaleX;
        var cameraScaleY = camera.ScaleY;
        var tileX = tile.Bounds.X;
        var tileY = tile.Bounds.Y;
        var tileWidth = tile.Bounds.Width;
        var tileHeight = tile.Bounds.Height;
        var sourceWidth = sourceDimensions.Width;
        var sourceHeight = sourceDimensions.Height;
        var maxSourceX = sourceWidth - 1;
        var maxSourceY = sourceHeight - 1;

        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * _layout.Stride;
            var destinationOffset = rowOffset + (left * 4);
            var worldY = (y - cameraOffsetY) / cameraScaleY;
            var sourceY = Math.Clamp(
                (int)((worldY - tileY) * sourceHeight / tileHeight),
                0,
                maxSourceY);
            var sourceRowOffset = sourceY * sourceWidth;

            var x = left;
            if (!hasSourcePixels)
            {
                if (Sse2.IsSupported)
                {
                    for (; x + 3 < right; x += 4)
                    {
                        WriteGrayPixels4(destination, destinationOffset, placeholder, placeholder, placeholder, placeholder);
                        destinationOffset += 16;
                    }
                }

                for (; x < right; x++)
                {
                    WritePackedGrayPixel(destination, destinationOffset, placeholder);
                    destinationOffset += 4;
                }

                continue;
            }

            if (Sse2.IsSupported)
            {
                for (; x + 3 < right; x += 4)
                {
                    var value0 = GetResidentTilePixelValue(
                        x,
                        cameraOffsetX,
                        cameraScaleX,
                        tileX,
                        tileWidth,
                        sourceWidth,
                        maxSourceX,
                        sourcePixels,
                        sourceRowOffset);
                    var value1 = GetResidentTilePixelValue(
                        x + 1,
                        cameraOffsetX,
                        cameraScaleX,
                        tileX,
                        tileWidth,
                        sourceWidth,
                        maxSourceX,
                        sourcePixels,
                        sourceRowOffset);
                    var value2 = GetResidentTilePixelValue(
                        x + 2,
                        cameraOffsetX,
                        cameraScaleX,
                        tileX,
                        tileWidth,
                        sourceWidth,
                        maxSourceX,
                        sourcePixels,
                        sourceRowOffset);
                    var value3 = GetResidentTilePixelValue(
                        x + 3,
                        cameraOffsetX,
                        cameraScaleX,
                        tileX,
                        tileWidth,
                        sourceWidth,
                        maxSourceX,
                        sourcePixels,
                        sourceRowOffset);
                    WriteGrayPixels4(destination, destinationOffset, value0, value1, value2, value3);
                    destinationOffset += 16;
                }
            }

            for (; x < right; x++)
            {
                var value = GetResidentTilePixelValue(
                    x,
                    cameraOffsetX,
                    cameraScaleX,
                    tileX,
                    tileWidth,
                    sourceWidth,
                    maxSourceX,
                    sourcePixels,
                    sourceRowOffset);
                WritePackedGrayPixel(destination, destinationOffset, value);
                destinationOffset += 4;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetResidentTilePixelValue(
        int screenX,
        double cameraOffsetX,
        double cameraScaleX,
        double tileX,
        double tileWidth,
        int sourceWidth,
        int maxSourceX,
        byte[]? sourcePixels,
        int sourceRowOffset)
    {
        var worldX = (screenX - cameraOffsetX) / cameraScaleX;
        var sourceX = Math.Clamp(
            (int)((worldX - tileX) * sourceWidth / tileWidth),
            0,
            maxSourceX);
        return sourcePixels![sourceRowOffset + sourceX];
    }

    private unsafe void DrawDefectPatch(byte* destination, SampleAnnotation annotation, CameraSnapshot camera)
    {
        // Patch geometry uses the pixel-payload dimensions. The GDI+ LockBits
        // source read was dead: its value was discarded because the display
        // value comes from DefectPixels via DefectOverlaySampler (ICW-321
        // F-008). Removing it also dissolves the dispose-vs-render race on the
        // pooled DefectBitmap and precedes the ICW-102 rescope.
        var imageWidth = annotation.DefectPixelWidth;
        var imageHeight = annotation.DefectPixelHeight;

        var imageLeftWorld = annotation.Bounds.X + ((annotation.Bounds.Width - imageWidth) / 2.0);
        var imageTopWorld = annotation.Bounds.Y + ((annotation.Bounds.Height - imageHeight) / 2.0);
        var imageRightWorld = imageLeftWorld + imageWidth;
        var imageBottomWorld = imageTopWorld + imageHeight;
        var topLeft = camera.WorldToScreen(imageLeftWorld, imageTopWorld);
        var bottomRight = camera.WorldToScreen(imageRightWorld, imageBottomWorld);
        var left = Math.Clamp((int)Math.Floor(topLeft.X), 0, _layout.Width);
        var top = Math.Clamp((int)Math.Floor(topLeft.Y), 0, _layout.Height);
        var right = Math.Clamp((int)Math.Ceiling(bottomRight.X), 0, _layout.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(bottomRight.Y), 0, _layout.Height);
        if (left >= right || top >= bottom)
        {
            return;
        }

        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * _layout.Stride;
            var destinationOffset = rowOffset + (left * 4);
            var worldY = (y - camera.OffsetY) / camera.ScaleY;

            for (var x = left; x < right; x++)
            {
                var worldX = (x - camera.OffsetX) / camera.ScaleX;
                var currentValue = destination[destinationOffset];
                var displayValue = DefectOverlaySampler.ResolveDisplayValue(currentValue, annotation, worldX, worldY);
                WritePackedGrayPixel(destination, destinationOffset, displayValue);
                destinationOffset += 4;
            }
        }
    }

    private static unsafe void WritePackedGrayPixel(byte* destination, int offset, byte value)
    {
        *(uint*)(destination + offset) = 0xFF000000u | ((uint)value * 0x00010101u);
    }

    private static unsafe void WriteGrayPixels4(
        byte* destination,
        int offset,
        byte value0,
        byte value1,
        byte value2,
        byte value3)
    {
        var packed = (uint)value0
            | ((uint)value1 << 8)
            | ((uint)value2 << 16)
            | ((uint)value3 << 24);
        var grayscale = Vector128.CreateScalar(packed).AsByte();
        var duplicatedChannels = Sse2.UnpackLow(grayscale, grayscale);
        var expanded = Sse2.UnpackLow(duplicatedChannels, duplicatedChannels);
        var alpha = Vector128.Create(
            (byte)0,
            0,
            0,
            byte.MaxValue,
            0,
            0,
            0,
            byte.MaxValue,
            0,
            0,
            0,
            byte.MaxValue,
            0,
            0,
            0,
            byte.MaxValue);
        Sse2.Store(destination + offset, Sse2.Or(expanded, alpha));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        lock (_lifetimeGate)
        {
            if (_view != IntPtr.Zero)
            {
                UnmapViewOfFile(_view);
                _view = IntPtr.Zero;
            }

            _section?.Dispose();
            _section = null;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileMappingW", SetLastError = true)]
    private static extern SafeFileMappingHandle CreateFileMapping(
        IntPtr file,
        IntPtr fileMappingAttributes,
        uint protection,
        uint maximumSizeHigh,
        uint maximumSizeLow,
        string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(
        SafeFileMappingHandle fileMapping,
        uint desiredAccess,
        uint fileOffsetHigh,
        uint fileOffsetLow,
        nuint bytesToMap);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr baseAddress);

    private sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeFileMappingHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
#endif
