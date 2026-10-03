namespace AutoPlay.Domain.Geometry;

/// <summary>A rectangle in physical screen pixels. <see cref="Right"/> and <see cref="Bottom"/> are exclusive.</summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public PixelSize Size => new(Width, Height);

    public PixelPoint TopLeft => new(Left, Top);

    public PixelPoint Center => new(Left + Width / 2, Top + Height / 2);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(PixelPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Contains(PixelRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public PixelRect Offset(int dx, int dy) => this with { Left = Left + dx, Top = Top + dy };

    /// <summary>Grows the rectangle by the given amounts on each side.</summary>
    public PixelRect Inflate(int horizontal, int vertical) =>
        new(Left - horizontal, Top - vertical, Width + 2 * horizontal, Height + 2 * vertical);

    /// <summary>Returns the intersection with <paramref name="other"/>, or an empty rectangle if they do not overlap.</summary>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? default
            : new PixelRect(left, top, right - left, bottom - top);
    }
}
