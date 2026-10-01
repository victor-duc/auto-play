using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Recording;

/// <summary>Geometry helpers for drawing, moving and resizing location rectangles on a capture.</summary>
public static class RectEditing
{
    /// <summary>Default size of a location created with a simple click.</summary>
    public static PixelSize DefaultLocationSize { get; } = new(48, 48);

    /// <summary>Minimum width and height of a location.</summary>
    public const int MinimumSize = 8;

    /// <summary>A drag shorter than this (in both directions) is treated as a simple click.</summary>
    public const int ClickThreshold = 4;

    /// <summary>Builds the rectangle spanned by two opposite corners, whatever the drag direction.</summary>
    public static PixelRect FromCorners(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Whether a drag from <paramref name="start"/> to <paramref name="end"/> is a simple click.</summary>
    public static bool IsClick(PixelPoint start, PixelPoint end) =>
        Math.Abs(end.X - start.X) < ClickThreshold && Math.Abs(end.Y - start.Y) < ClickThreshold;

    /// <summary>A rectangle of the given size centered on a point, kept inside the bounds.</summary>
    public static PixelRect CenteredAt(PixelPoint center, PixelSize size, PixelSize bounds) =>
        KeepInside(new PixelRect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height), bounds);

    /// <summary>
    /// Moves the rectangle so that it lies within <c>(0, 0, bounds)</c>, shrinking it only if it is larger than the bounds.
    /// </summary>
    public static PixelRect KeepInside(PixelRect rect, PixelSize bounds)
    {
        var width = Math.Min(rect.Width, bounds.Width);
        var height = Math.Min(rect.Height, bounds.Height);
        var left = Math.Clamp(rect.Left, 0, bounds.Width - width);
        var top = Math.Clamp(rect.Top, 0, bounds.Height - height);
        return new PixelRect(left, top, width, height);
    }

    /// <summary>Clips the rectangle to <c>(0, 0, bounds)</c> and enforces the minimum size.</summary>
    public static PixelRect Normalize(PixelRect rect, PixelSize bounds)
    {
        var clipped = rect.Intersect(new PixelRect(0, 0, bounds.Width, bounds.Height));
        if (clipped.IsEmpty)
        {
            clipped = rect with { Width = 0, Height = 0 };
        }

        return KeepInside(
            clipped with { Width = Math.Max(clipped.Width, MinimumSize), Height = Math.Max(clipped.Height, MinimumSize) },
            bounds);
    }

    /// <summary>Returns the part of <paramref name="rect"/> at <paramref name="point"/>, corners first.</summary>
    /// <param name="tolerance">Distance, in pixels, within which a corner is grabbed.</param>
    public static RectHandle HitTest(PixelRect rect, PixelPoint point, int tolerance)
    {
        bool Near(int value, int target) => Math.Abs(value - target) <= tolerance;

        if (Near(point.X, rect.Left) && Near(point.Y, rect.Top))
        {
            return RectHandle.TopLeft;
        }

        if (Near(point.X, rect.Right) && Near(point.Y, rect.Top))
        {
            return RectHandle.TopRight;
        }

        if (Near(point.X, rect.Left) && Near(point.Y, rect.Bottom))
        {
            return RectHandle.BottomLeft;
        }

        if (Near(point.X, rect.Right) && Near(point.Y, rect.Bottom))
        {
            return RectHandle.BottomRight;
        }

        return rect.Contains(point) ? RectHandle.Body : RectHandle.None;
    }

    /// <summary>Moves a corner of <paramref name="original"/> to <paramref name="position"/>; the opposite corner stays fixed.</summary>
    public static PixelRect Resize(PixelRect original, RectHandle handle, PixelPoint position, PixelSize bounds)
    {
        var x = Math.Clamp(position.X, 0, bounds.Width);
        var y = Math.Clamp(position.Y, 0, bounds.Height);
        int left = original.Left, top = original.Top, right = original.Right, bottom = original.Bottom;

        switch (handle)
        {
            case RectHandle.TopLeft:
                left = Math.Min(x, right - MinimumSize);
                top = Math.Min(y, bottom - MinimumSize);
                break;
            case RectHandle.TopRight:
                right = Math.Max(x, left + MinimumSize);
                top = Math.Min(y, bottom - MinimumSize);
                break;
            case RectHandle.BottomLeft:
                left = Math.Min(x, right - MinimumSize);
                bottom = Math.Max(y, top + MinimumSize);
                break;
            case RectHandle.BottomRight:
                right = Math.Max(x, left + MinimumSize);
                bottom = Math.Max(y, top + MinimumSize);
                break;
            default:
                return original;
        }

        return KeepInside(new PixelRect(left, top, right - left, bottom - top), bounds);
    }

    /// <summary>Returns the nearest point inside the rectangle.</summary>
    public static PixelPoint Clamp(PixelPoint point, PixelRect rect) =>
        new(Math.Clamp(point.X, rect.Left, rect.Right - 1), Math.Clamp(point.Y, rect.Top, rect.Bottom - 1));
}
