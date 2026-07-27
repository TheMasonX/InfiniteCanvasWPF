using System.Drawing;
using System.Drawing.Imaging;

namespace InfiniteCanvas.Rendering;

internal static class DefectTemplateFactory
{
    public static IReadOnlyList<SampleImageGenerator.DefectTemplate> Build(int count, SampleImageGenerator.DeterministicRandom random)
    {
        var pool = new SampleImageGenerator.DefectTemplate[count];
        for (var index = 0; index < count; index++)
        {
            var aspect = 0.45 + (random.NextDouble() * 1.95);
            var templateWidth = random.Next(156, 276);
            var templateHeight = Math.Clamp((int)Math.Round(templateWidth / aspect), 132, 304);
            var pixels = SampleImageGenerator.GenerateCenteredDefectPixels(templateWidth, templateHeight, random);
#if WINDOWS
            pool[index] = new SampleImageGenerator.DefectTemplate(
                templateWidth,
                templateHeight,
                pixels,
                CreateBitmapFromPixels(templateWidth, templateHeight, pixels));
#else
            pool[index] = new SampleImageGenerator.DefectTemplate(templateWidth, templateHeight, pixels);
#endif
        }

        return pool;
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
}
