#if WINDOWS
using InfiniteCanvas.Core;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InfiniteCanvas.Rendering;

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
        Func<SampleImageTile, bool>? tryReserveCacheEntry = null,
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
        Func<SampleImageTile, bool>? tryReserveCacheEntry,
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
        var shouldGeneratePixels = tile.IsMipGenerated(mipLevel)
            || tile.ShouldGenerateForPixelSize(camera, minimumSparseTilePixelSize);
        var hasSourcePixels = shouldGeneratePixels
            && (mipLevel == 0
                ? tile.TryGetPixelsNonBlocking(
                    out sourcePixels,
                    tryReserveCacheEntry is null ? null : () => tryReserveCacheEntry(tile))
                : tile.TryGetPixelsNonBlocking(mipLevel, out sourcePixels));
        var sourceDimensions = BackgroundTileMipPolicy.GetDimensions(tile.PixelWidth, tile.PixelHeight, mipLevel);
        var placeholder = tile.PlaceholderValue;

        for (var y = top; y < bottom; y++)
        {
            var worldY = (y - camera.OffsetY) / camera.ScaleY;
            var sourceY = Math.Clamp(
                (int)((worldY - tile.Bounds.Y) * sourceDimensions.Height / tile.Bounds.Height),
                0,
                sourceDimensions.Height - 1);

            for (var x = left; x < right; x++)
            {
                var worldX = (x - camera.OffsetX) / camera.ScaleX;
                var sourceX = Math.Clamp(
                    (int)((worldX - tile.Bounds.X) * sourceDimensions.Width / tile.Bounds.Width),
                    0,
                    sourceDimensions.Width - 1);
                var value = hasSourcePixels
                    ? sourcePixels![(sourceY * sourceDimensions.Width) + sourceX]
                    : placeholder;
                var offset = _layout.GetPixelOffset(x, y);
                destination[offset] = value;
                destination[offset + 1] = value;
                destination[offset + 2] = value;
                destination[offset + 3] = byte.MaxValue;
            }
        }
    }

    private unsafe void DrawDefectPatch(byte* destination, SampleAnnotation annotation, CameraSnapshot camera)
    {
        var bitmap = annotation.DefectBitmap;
        if (bitmap is null)
        {
            return;
        }

        var imageLeftWorld = annotation.Bounds.X + ((annotation.Bounds.Width - bitmap.Width) / 2.0);
        var imageTopWorld = annotation.Bounds.Y + ((annotation.Bounds.Height - bitmap.Height) / 2.0);
        var imageRightWorld = imageLeftWorld + bitmap.Width;
        var imageBottomWorld = imageTopWorld + bitmap.Height;
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

        var bitmapBounds = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(bitmapBounds, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            var source = (byte*)bitmapData.Scan0;
            for (var y = top; y < bottom; y++)
            {
                var worldY = (y - camera.OffsetY) / camera.ScaleY;
                var sourceY = Math.Clamp((int)(worldY - imageTopWorld), 0, bitmap.Height - 1);
                var sourceRow = source + (sourceY * bitmapData.Stride);

                for (var x = left; x < right; x++)
                {
                    var worldX = (x - camera.OffsetX) / camera.ScaleX;
                    var sourceX = Math.Clamp((int)(worldX - imageLeftWorld), 0, bitmap.Width - 1);
                    var value = sourceRow[sourceX * 3];
                    var offset = _layout.GetPixelOffset(x, y);
                    var currentValue = destination[offset];
                    var displayValue = DefectOverlaySampler.ResolveDisplayValue(currentValue, annotation, worldX, worldY);
                    destination[offset] = displayValue;
                    destination[offset + 1] = displayValue;
                    destination[offset + 2] = displayValue;
                    destination[offset + 3] = byte.MaxValue;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
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
