using AutoPlay.Domain.Imaging;

namespace AutoPlay.Application.Tests.Fakes;

internal static class TestImages
{
    /// <summary>Creates an image where each pixel's blue channel encodes its position, so crops can be checked.</summary>
    public static RawImage Gradient(int width, int height)
    {
        var pixels = new byte[width * height * RawImage.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * RawImage.BytesPerPixel;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 3] = 0xFF;
            }
        }

        return new RawImage(width, height, pixels);
    }

    public static RawImage Solid(int width, int height, byte value = 0x80) =>
        new(width, height, Enumerable.Repeat(value, width * height * RawImage.BytesPerPixel).ToArray());
}
