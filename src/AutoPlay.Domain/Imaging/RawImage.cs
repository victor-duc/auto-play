using AutoPlay.Domain.Geometry;

namespace AutoPlay.Domain.Imaging;

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

    /// <summary>
    /// Returns a reduced copy that fits in <paramref name="maxWidth"/> × <paramref name="maxHeight"/>, keeping the
    /// proportions; each pixel is the average of the source pixels it covers. Returns this image if it already fits.
    /// </summary>
    public RawImage ScaleDown(int maxWidth, int maxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);
        var scale = Math.Min((double)maxWidth / Width, (double)maxHeight / Height);
        if (scale >= 1)
        {
            return this;
        }

        var width = Math.Max(1, (int)Math.Round(Width * scale));
        var height = Math.Max(1, (int)Math.Round(Height * scale));
        var result = new byte[width * height * BytesPerPixel];
        Span<int> sums = stackalloc int[BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            var top = y * Height / height;
            var bottom = Math.Max(top + 1, (y + 1) * Height / height);
            for (var x = 0; x < width; x++)
            {
                var left = x * Width / width;
                var right = Math.Max(left + 1, (x + 1) * Width / width);
                sums.Clear();
                for (var sy = top; sy < bottom; sy++)
                {
                    for (var sx = left; sx < right; sx++)
                    {
                        var i = sy * Stride + sx * BytesPerPixel;
                        for (var c = 0; c < BytesPerPixel; c++)
                        {
                            sums[c] += Pixels[i + c];
                        }
                    }
                }

                var count = (bottom - top) * (right - left);
                var o = (y * width + x) * BytesPerPixel;
                for (var c = 0; c < BytesPerPixel; c++)
                {
                    result[o + c] = (byte)(sums[c] / count);
                }
            }
        }

        return new RawImage(width, height, result);
    }

    /// <summary>Returns a copy with the outline of a rectangle drawn on it, clipped to the image.</summary>
    public RawImage WithRectangle(PixelRect rect, byte red, byte green, byte blue, int thickness = 2)
    {
        var pixels = (byte[])Pixels.Clone();
        var bounds = new PixelRect(0, 0, Width, Height);
        var inner = rect.Inflate(-thickness, -thickness);
        var area = rect.Intersect(bounds);
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                if (!inner.IsEmpty && inner.Contains(new PixelPoint(x, y)))
                {
                    continue;
                }

                var i = y * Stride + x * BytesPerPixel;
                pixels[i] = blue;
                pixels[i + 1] = green;
                pixels[i + 2] = red;
                pixels[i + 3] = 0xFF;
            }
        }

        return new RawImage(Width, Height, pixels);
    }
}
