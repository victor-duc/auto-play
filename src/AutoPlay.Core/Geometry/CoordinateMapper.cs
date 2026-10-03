namespace AutoPlay.Core.Geometry;

/// <summary>
/// Converts between normalized coordinates (relative to a target region) and physical screen pixels,
/// using the proportional positioning mode.
/// </summary>
/// <remarks>
/// A point designates a pixel and is normalized from the pixel's center, so that conversions round-trip exactly
/// and points scale symmetrically. Rectangles are normalized from their edges.
/// </remarks>
public static class CoordinateMapper
{
    public static PixelPoint ToPixel(NormalizedPoint point, PixelRect region) =>
        new(
            region.Left + Math.Clamp((int)Math.Floor(point.X * region.Width), 0, region.Width - 1),
            region.Top + Math.Clamp((int)Math.Floor(point.Y * region.Height), 0, region.Height - 1));

    public static PixelRect ToPixel(NormalizedRect rect, PixelRect region)
    {
        var left = region.Left + Round(rect.X * region.Width);
        var top = region.Top + Round(rect.Y * region.Height);
        var right = region.Left + Round((rect.X + rect.Width) * region.Width);
        var bottom = region.Top + Round((rect.Y + rect.Height) * region.Height);
        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public static NormalizedPoint ToNormalized(PixelPoint point, PixelRect region)
    {
        EnsureNotEmpty(region);
        return new NormalizedPoint(
            (point.X - region.Left + 0.5) / region.Width,
            (point.Y - region.Top + 0.5) / region.Height);
    }

    public static NormalizedRect ToNormalized(PixelRect rect, PixelRect region)
    {
        EnsureNotEmpty(region);
        return new NormalizedRect(
            (rect.Left - region.Left) / (double)region.Width,
            (rect.Top - region.Top) / (double)region.Height,
            rect.Width / (double)region.Width,
            rect.Height / (double)region.Height);
    }

    /// <summary>Rounds half away from zero (12.5 → 13), which is more intuitive for pixels than banker's rounding.</summary>
    internal static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static void EnsureNotEmpty(PixelRect region)
    {
        if (region.IsEmpty)
        {
            throw new ArgumentException("The target region must not be empty.", nameof(region));
        }
    }
}
