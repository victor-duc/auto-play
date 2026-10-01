using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;

namespace AutoPlay.Vision.Tests;

internal static class TestPatterns
{
    /// <summary>Random gray pixels (opaque).</summary>
    public static RawImage Noise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * RawImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RawImage.BytesPerPixel)
        {
            var value = (byte)random.Next(256);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
            pixels[i + 3] = 0xFF;
        }

        return new RawImage(width, height, pixels);
    }

    /// <summary>Random gray blocks, which survive resizing better than per-pixel noise.</summary>
    public static RawImage Blocks(int width, int height, int blockSize, int seed)
    {
        var random = new Random(seed);
        var columns = (width + blockSize - 1) / blockSize;
        var rows = (height + blockSize - 1) / blockSize;
        var values = new byte[columns * rows];
        random.NextBytes(values);

        var pixels = new byte[width * height * RawImage.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = values[(y / blockSize) * columns + x / blockSize];
                var i = (y * width + x) * RawImage.BytesPerPixel;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = value;
                pixels[i + 3] = 0xFF;
            }
        }

        return new RawImage(width, height, pixels);
    }

    /// <summary>Nearest-neighbor upscale by an integer factor.</summary>
    public static RawImage Upscale(RawImage image, int factor)
    {
        var width = image.Width * factor;
        var height = image.Height * factor;
        var pixels = new byte[width * height * RawImage.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var source = ((y / factor) * image.Width + x / factor) * RawImage.BytesPerPixel;
                Buffer.BlockCopy(image.Pixels, source, pixels, (y * width + x) * RawImage.BytesPerPixel, RawImage.BytesPerPixel);
            }
        }

        return new RawImage(width, height, pixels);
    }

    public static void FillRect(RawImage image, PixelRect area, byte value)
    {
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                var i = (y * image.Width + x) * RawImage.BytesPerPixel;
                image.Pixels[i] = image.Pixels[i + 1] = image.Pixels[i + 2] = value;
            }
        }
    }
}
