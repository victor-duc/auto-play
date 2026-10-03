using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Imaging;

/// <summary>
/// An uncompressed 32-bit BGRA image, top-down, with no row padding (stride = width × 4).
/// Used to exchange pixels between capture, matching and storage without depending on a UI framework.
/// </summary>
public sealed class RawImage
{
    public const int BytesPerPixel = 4;

    public RawImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height * BytesPerPixel)
        {
            throw new ArgumentException(
                $"Expected {width * height * BytesPerPixel} bytes for a {width}x{height} BGRA image, got {pixels.Length}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * BytesPerPixel;

    public PixelSize Size => new(Width, Height);

    /// <summary>The pixel data, in BGRA order.</summary>
    public byte[] Pixels { get; }

    /// <summary>Returns a copy of the given area. The area must lie within the image.</summary>
    public RawImage Crop(PixelRect area)
    {
        if (area.IsEmpty || !new PixelRect(0, 0, Width, Height).Contains(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "The area must lie within the image.");
        }

        var result = new byte[area.Width * area.Height * BytesPerPixel];
        var rowLength = area.Width * BytesPerPixel;
        for (var y = 0; y < area.Height; y++)
        {
            Buffer.BlockCopy(Pixels, (area.Top + y) * Stride + area.Left * BytesPerPixel, result, y * rowLength, rowLength);
        }

        return new RawImage(area.Width, area.Height, result);
    }
}
