using SkiaSharp;

namespace UvcCaptureSim.Core.Rendering;

/// <summary>Packs a SkiaSharp BGRA frame into the top-down BGR24 buffer softcam's sender API expects.</summary>
public static class PixelConverter
{
    public static int Bgr24Size(int width, int height) => width * height * 3;

    /// <summary>
    /// Drops the alpha byte from BGRA. softcam wants tightly packed B,G,R, top-down, width a multiple of 4.
    /// </summary>
    public static unsafe void Bgra8888ToBgr24(SKBitmap source, byte[] destination)
    {
        var width = source.Width;
        var height = source.Height;
        var stride = source.RowBytes;
        var pixels = (byte*)source.GetPixels();
        if (pixels is null)
            throw new InvalidOperationException("The source bitmap has no readable pixels.");

        fixed (byte* destinationBase = destination)
        {
            var dst = destinationBase;
            for (var y = 0; y < height; y++)
            {
                var row = pixels + (y * stride);
                for (var x = 0; x < width; x++)
                {
                    dst[0] = row[0];
                    dst[1] = row[1];
                    dst[2] = row[2];
                    row += 4;
                    dst += 3;
                }
            }
        }
    }
}
