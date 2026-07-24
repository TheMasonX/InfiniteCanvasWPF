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
