namespace AutoPlay.Core.Geometry;

/// <summary>
/// Converts between normalized coordinates (relative to a target region) and physical screen pixels,
/// using the proportional positioning mode.
/// </summary>
public static class CoordinateMapper
{
    public static PixelPoint ToPixel(NormalizedPoint point, PixelRect region) =>
        new(
            region.Left + Round(point.X * region.Width),
            region.Top + Round(point.Y * region.Height));

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
            (point.X - region.Left) / (double)region.Width,
            (point.Y - region.Top) / (double)region.Height);
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
